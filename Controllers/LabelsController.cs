using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecordsMaster.Models;
using RecordsMaster.Data;
using RecordsMaster.Services;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Threading.Tasks;

namespace RecordsMaster.Controllers
{
    public class LabelsController : Controller
    {
        private readonly AppDbContext _context;
        
        private readonly PDFPrintService _pdfPrintService;

        public LabelsController(AppDbContext context, PDFPrintService pdfPrintService)
        {

            _context = context;
            _pdfPrintService = pdfPrintService;
            

        }

        // Action to return the view
        public async Task<IActionResult> GenerateLabelsForm()
        {
            var recentUploadDates = await _context.RecordItems
                .GroupBy(r => r.CreatedOn.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Date)
                .Take(10)
                .ToListAsync();

            ViewBag.RecentUploadDates = recentUploadDates;
            return View();
        }

        public async Task<IActionResult> GenerateLabelsByDate(DateTime date)
        {
            var records = await _context.RecordItems
                .Where(r => r.CreatedOn.Date == date.Date)
                .OrderBy(r => r.BarCode)
                .ToListAsync();

            if (records.Count == 0)
            {
                ViewBag.RecentUploadDates = await _context.RecordItems
                    .GroupBy(r => r.CreatedOn.Date)
                    .Select(g => new { Date = g.Key, Count = g.Count() })
                    .OrderByDescending(x => x.Date)
                    .Take(10)
                    .ToListAsync();
                ViewData["ErrorMessage"] = $"No records found for {date:MM/dd/yyyy}.";
                return View("GenerateLabelsForm");
            }

            var (pdfBytes, fileName) = _pdfPrintService.GenerateLabelsPdfForDownload(records);
            return File(pdfBytes, "application/pdf", fileName);
        }

        // Generate the labels PDF. OMG THIS TOOK FOREVER TO FIGURE OUT
        public async Task<IActionResult> GenerateLabels(string barcode_start, string barcode_end)
        {
            if (string.IsNullOrEmpty(barcode_start) || string.IsNullOrEmpty(barcode_end))
            {
                ViewData["ErrorMessage"] = "Both barcode start and end values must be provided.";
                return View("GenerateLabelsForm");
            }

            // Find both barcode records to ensure they exist
            var startRecord = await _context.RecordItems.FirstOrDefaultAsync(r => r.BarCode == barcode_start);
            var endRecord = await _context.RecordItems.FirstOrDefaultAsync(r => r.BarCode == barcode_end);

            if (startRecord == null || endRecord == null)
            {
                ViewData["ErrorMessage"] = "No records found in the specified barcode range.";
                return View("GenerateLabelsForm");
            }

            // Determine barcode range alphabetically (because barcodes are strings)
            string lowerBarcode = string.Compare(barcode_start, barcode_end, StringComparison.OrdinalIgnoreCase) <= 0
                ? barcode_start
                : barcode_end;

            string upperBarcode = string.Compare(barcode_start, barcode_end, StringComparison.OrdinalIgnoreCase) >= 0
                ? barcode_start
                : barcode_end;

            // Fetch records in the inclusive barcode range
            var records = await _context.RecordItems
                                        .Where(r => string.Compare(r.BarCode, lowerBarcode) >= 0 &&
                                                    string.Compare(r.BarCode, upperBarcode) <= 0)
                                        .OrderBy(r => r.BarCode)
                                        .ToListAsync();

            if (records.Count == 0)
            {
                ViewData["ErrorMessage"] = "No records found in the specified barcode range.";
                return View("GenerateLabelsForm");
            }

            var (pdfBytes, fileName) = _pdfPrintService.GenerateLabelsPdfForDownload(records);

            // Return PDF file as download with filename "firstBarcode - lastBarcode.pdf"
            return File(pdfBytes, "application/pdf", fileName);
 
        }

    }
}
