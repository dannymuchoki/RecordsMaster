using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using RecordsMaster.Models;
using RecordsMaster.Data;
using RecordsMaster.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Diagnostics;

namespace RecordsMaster.Controllers
{
    [Authorize]
    public class UploadController : Controller
    {
        private readonly AppDbContext _context;

        private readonly LabelPrintService _labelPrintService; 

        public UploadController(AppDbContext context, LabelPrintService labelPrintService)
        {
            _labelPrintService = labelPrintService;
            _context = context;
        }

        // GET: RecordItems/Upload
        public IActionResult Upload()
        {
            return View();
        }
        
        // This controller adds new record items via a CSV file. 
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                ModelState.AddModelError("file", "Please upload a valid CSV file.");
                return View();
            }

            List<string> rowErrors = new List<string>();
            int rowNumber = 2; // Start counting from the first data row after the header
            List<RecordItemModel> validRecords = new List<RecordItemModel>();

            // Query the DB once to get the last saved barcode.
            //string lastBarcodeInDb = _context.RecordItems.OrderByDescending(r => r.BarCode).Select(r => r.BarCode).FirstOrDefault();

            var lastBarcodeInDb = _context.RecordItems.OrderByDescending(r => r.CreatedOn).Select(r => r.BarCode).FirstOrDefault();

            // We'll track the most recent barcode as we process the CSV.
            string lastBarcode = lastBarcodeInDb;

            try
            {
                using (var stream = new StreamReader(file.OpenReadStream()))
                {
                    var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
                    {
                        HasHeaderRecord = true,
                        HeaderValidated = null,
                        MissingFieldFound = null
                    };

                    using (var csv = new CsvReader(stream, csvConfig))
                    {
                        // Register the map which ignores the ID property.
                        csv.Context.RegisterClassMap<CsvHelperMap>();

                        // Skip header
                        csv.Read();
                        csv.ReadHeader();

                        while (csv.Read())
                        {
                            // Validation: first column must be integer
                            string firstField = csv.GetField(0);
                            if (!int.TryParse(firstField, out _))
                            {
                                rowErrors.Add($"Row {rowNumber} error: The first column value ('{firstField}') is not a valid integer.");
                                rowNumber++;
                                continue;
                            }

                            // Validation: record type must be "PS", "FC", "EX"
                            var recordTypeField = csv.GetField(2);
                            List<string> validValues = new List<string> { "PS", "FC", "EX", "FS" };
                            if (!validValues.Contains(recordTypeField))
                            {
                                rowErrors.Add($"Row {rowNumber} error: '{recordTypeField}' is not a valid record type (FC, PS, or EX).");
                                rowNumber++;
                                continue;
                            }
                            else
                            {
                                Debug.WriteLine($"The value is recognized: {recordTypeField}");
                            }

                            // Location of the record
                            var locationField = csv.GetField(4);

                            // Validation: the sixth column is a valid DateTime
                            var dateField = csv.GetField(6);
                            if (!DateTime.TryParse(dateField, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                            {
                                rowErrors.Add($"Row {rowNumber} skipped: The sixth column item ('{dateField}') is not a valid date.");
                                rowNumber++;
                                continue;
                            }

                            // Map the CSV row to RecordItemModel; note that the ID property is ignored by the map.
                            var recordItem = csv.GetRecord<RecordItemModel>();

                            // Generate a new unique ID for each record (since the CSV has no ID column)
                            recordItem.ID = Guid.NewGuid();

                            // Generate the barcode by getting the next barcode based on the last value.
                            recordItem.BarCode = GetNextBarcode(lastBarcode);

                            // Update our local variable so the next call knows the last value after increment.
                            lastBarcode = recordItem.BarCode;

                            validRecords.Add(recordItem);
                            rowNumber++;
                        }
                    }
                }

                // If any row errors were gathered, show them in the view
                if (rowErrors.Any())
                {
                    ModelState.AddModelError("file", "Some rows contained errors. Please review the details below:");
                    foreach (var error in rowErrors)
                    {
                        ModelState.AddModelError(string.Empty, error);
                    }
                    return View();
                }

                // If no valid records remain after validation, warn the user
                if (!validRecords.Any())
                {
                    ModelState.AddModelError("file", "No valid records were found in the CSV file. Please check your file and try again.");
                    return View();
                }

                // Save the valid records to the database
                foreach (var recordItem in validRecords)
                {
                    _context.RecordItems.Add(recordItem);
                }

                await _context.SaveChangesAsync();

                // Print at upload
                _labelPrintService.PrintLabels(validRecords);

                return RedirectToAction("Index", "RecordItems");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("file", $"An error occurred while uploading the file: {ex.Message}");
                return View();
            }
        }

        /// <summary>
        /// Gets the next barcode based on an input last barcode value.
        /// The barcode format is YY-XXXXXX where:
        ///   - The first two digits are the last two of the current year.
        ///   - The numeric part is incremented if the input barcode is in the current year.
        ///   - If the input barcode is null/empty or its year portion is one less than current, the sequence starts at 000001.
        /// </summary>
        /// <param name="lastBarcode">The last barcode value (could be null or empty).</param>
        /// <returns>A new barcode string.</returns>
        private string GetNextBarcode(string lastBarcode)
        {
            // Get two-digit current year (e.g., 23 for 2023)
            int currentYear = DateTime.Now.Year;
            int currentYearSuffix = currentYear % 100;
            string newYearPart = currentYearSuffix.ToString("D2");

            int newSequence = 1;

            // If there is no previous barcode or it is empty, start at 00001.
            if (string.IsNullOrEmpty(lastBarcode))
            {
                newSequence = 1;
            }
            else
            {
                // Expected format "YY-XXXXX" - 8 because the string is 8 characters long
                if (lastBarcode.Length >= 8)
                {
                    // Extract the two-digit year from the barcode.
                    if (int.TryParse(lastBarcode.Substring(0, 2), out int previousYear))
                    {
                        // If the previous barcode's year is exactly one less than the current year suffix, reset the sequence.
                        if (previousYear == currentYearSuffix - 1)
                        {
                            newSequence = 1;
                        }
                        // If the previous barcode's year matches the current year, increment the sequence.
                        else if (previousYear == currentYearSuffix)
                        {
                            string sequencePart = lastBarcode.Substring(3);
                            if (int.TryParse(sequencePart, out int previousSequence))
                            {
                                newSequence = previousSequence + 1;
                            }
                            else
                            {
                                newSequence = 1;
                            }
                        }
                        else
                        {
                            // For any other previous year, reset.
                            newSequence = 1;
                        }
                    }
                    else
                    {
                        newSequence = 1;
                    }
                }
                else
                {
                    newSequence = 1;
                }
            }

            string sequencePartFormatted = newSequence.ToString("D5");
            return $"{newYearPart}-{sequencePartFormatted}";
        }
    }
}