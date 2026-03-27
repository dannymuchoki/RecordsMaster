using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using RecordsMaster.Models;
using RecordsMaster.Data;
using System;

namespace RecordsMaster.Controllers
{
    [Authorize]
    public class UpdateController : Controller
    {
        private readonly AppDbContext _context;

        public UpdateController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Update/Upload
        public IActionResult Upload()
        {
            return View();
        }

        // POST: Update/Upload
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                ModelState.AddModelError("file", "Please upload a valid CSV file.");
                return View();
            }

            // Each entry holds the original row fields plus an "Error" column
            var errorRows = new List<Dictionary<string, string>>();
            int rowNumber = 2; // Assuming header is row 1
            string[]? headers = null;

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
                        csv.Read();
                        csv.ReadHeader();
                        headers = csv.HeaderRecord;

                        while (csv.Read())
                        {
                            // Capture all raw field values for this row
                            var rawFields = new Dictionary<string, string>();
                            if (headers != null)
                            {
                                foreach (var header in headers)
                                    rawFields[header] = csv.GetField(header) ?? string.Empty;
                            }

                            string barcodeField = csv.GetField(1)!;

                            if (string.IsNullOrWhiteSpace(barcodeField))
                            {
                                rawFields["Error"] = $"Row {rowNumber}: BarCode is empty.";
                                errorRows.Add(rawFields);
                                rowNumber++;
                                continue;
                            }

                            // For updates, just by barcode works
                            var record = _context.RecordItems.FirstOrDefault(r => r.BarCode == barcodeField);
                            if (record == null)
                            {
                                rawFields["Error"] = $"Row {rowNumber}: No record found with BarCode '{barcodeField}'.";
                                errorRows.Add(rawFields);
                                rowNumber++;
                                continue;
                            }

                            if (csv.HeaderRecord != null && csv.HeaderRecord.Length > 2)
                            {
                                var locationField = csv.GetField(3);
                                if (!string.IsNullOrWhiteSpace(locationField))
                                {
                                    record.Location = locationField;
                                }

                                var boxNumberField = csv.GetField(4);
                                if (!string.IsNullOrWhiteSpace(boxNumberField) && int.TryParse(boxNumberField, out var boxnumber))
                                {
                                    record.BoxNumber = boxnumber;
                                }

                                var digitizedField = csv.GetField(5);
                                if (!string.IsNullOrWhiteSpace(digitizedField) && bool.TryParse(digitizedField, out var digitized))
                                {
                                    record.Digitized = digitized;
                                }
                            }

                            rowNumber++;
                        }
                    }
                }

                // Save all valid records regardless of errors
                await _context.SaveChangesAsync();

                if (errorRows.Any())
                {
                    // Build error CSV in memory and return as download
                    var csvStream = new MemoryStream();
                    using (var writer = new StreamWriter(csvStream, leaveOpen: true))
                    using (var errorCsv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                    {
                        // Write header: original columns + Error
                        var errorHeaders = (headers ?? []).Append("Error").ToList();
                        foreach (var h in errorHeaders)
                        {
                            errorCsv.WriteField(h);
                        }
                        errorCsv.NextRecord();

                        foreach (var row in errorRows)
                        {
                            foreach (var h in errorHeaders)
                            {
                                errorCsv.WriteField(row.TryGetValue(h, out var val) ? val : string.Empty);
                            }
                            errorCsv.NextRecord();
                        }
                    }

                    csvStream.Position = 0;
                    TempData["Warning"] = $"{rowNumber - 2 - errorRows.Count} record(s) updated. {errorRows.Count} row(s) had errors — see downloaded errors.csv.";
                    return File(csvStream, "text/csv", "errors.csv");
                }

                TempData["Success"] = "Records updated successfully.";
                return RedirectToAction("Index", "RecordItems");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("file", $"An error occurred while processing the file: {ex.Message}");
                return View();
            }
        }
    }
}