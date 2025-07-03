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

            List<string> rowErrors = new List<string>();
            int rowNumber = 2; // Assuming header is row 1

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

                        while (csv.Read())
                        {
                            string cisField = csv.GetField(0);
                            string barcodeField = csv.GetField(1);

                            if (!int.TryParse(cisField, out int cis))
                            {
                                rowErrors.Add($"Row {rowNumber}: CIS '{cisField}' is not a valid integer.");
                                rowNumber++;
                                continue;
                            }

                            if (string.IsNullOrWhiteSpace(barcodeField))
                            {
                                rowErrors.Add($"Row {rowNumber}: BarCode is empty.");
                                rowNumber++;
                                continue;
                            }

                            // Find the record by CIS and BarCode
                            var record = _context.RecordItems.FirstOrDefault(r => r.CIS == cis && r.BarCode == barcodeField);
                            if (record == null)
                            {
                                rowErrors.Add($"Row {rowNumber}: No record found with CIS '{cis}' and BarCode '{barcodeField}'.");
                                rowNumber++;
                                continue;
                            }

                            // Example: update the Location field from the fifth column if present
                            if (csv.HeaderRecord != null && csv.HeaderRecord.Length > 2)
                            {
                                var locationField = csv.GetField(5);
                                if (!string.IsNullOrWhiteSpace(locationField))
                                {
                                    record.Location = locationField;
                                }
                                
                            }

                            // Add more updates here as needed, e.g., record.RecordType = csv.GetField(3);

                            rowNumber++;
                        }
                    }
                }

                if (rowErrors.Any())
                {
                    ModelState.AddModelError("file", "Some rows contained errors. Please review the details below:");
                    foreach (var error in rowErrors)
                    {
                        ModelState.AddModelError(string.Empty, error);
                    }
                    return View();
                }

                await _context.SaveChangesAsync();
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