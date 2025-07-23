using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecordsMaster.Models;
using RecordsMaster.Data;
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

        //private static List<RecordItemModel>? _recordsToPrint;
        private static int _recordIndex;

        public LabelsController(AppDbContext context)
        {
            
            _context = context;
            
        }

        // Action to return the view
        public IActionResult GenerateLabelsForm()
        {
            return View();
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

            // Generate PDF
            using var document = new PrintDocument();
            document.PrintPage += (sender, e) => PrintLabels(e, records);
            document.PrinterSettings.PrinterName = "Microsoft Print to PDF";
            document.PrinterSettings.PrintToFile = true;
            document.PrinterSettings.PrintFileName = Path.Combine(Path.GetTempPath(), "labels.pdf");

            document.Print();

            var pdfBytes = System.IO.File.ReadAllBytes(document.PrinterSettings.PrintFileName);
            return File(pdfBytes, "application/pdf", $"labels {barcode_start}-{barcode_end}.pdf");
        }

        // This works on my computer, but might not work on a server. May need to use a library.  
        private void PrintLabels(PrintPageEventArgs e, List<RecordItemModel> records)
        {
            float labelWidth = 4 * 100; // 4 inches
            float labelHeight = 1.33f * 100; // 1.33 inches
            float margin = 0.2f * 100; // 0.2 inches margin

            int labelsPerRow = 2;
            int labelsPerColumn = 7;
            int labelsPerPage = labelsPerRow * labelsPerColumn;
            
            while (_recordIndex < records.Count)
            {
                int currentPageIndex = _recordIndex / labelsPerPage;
                int startIndex = currentPageIndex * labelsPerPage;
                int endIndex = Math.Min(startIndex + labelsPerPage, records.Count);
                
                int recordIndex = startIndex;

                for (int row = 0; row < labelsPerColumn; row++)
                {
                    for (int col = 0; col < labelsPerRow; col++)
                    {
                        if (recordIndex >= endIndex)
                            break;

                        float x = margin + col * (labelWidth + margin);
                        float y = margin + row * (labelHeight + margin);

                        var record = records[recordIndex++];
                        e.Graphics.DrawRectangle(Pens.Black, x, y, labelWidth, labelHeight);
                        e.Graphics.DrawString($"CIS: {record.CIS}\nBarCode: {record.BarCode}\nRecordType: {record.RecordType}", new Font("Arial", 14), Brushes.Black, x + 10, y + 10);
                    }
                }

                _recordIndex = endIndex;

                if (_recordIndex < records.Count)
                {
                    e.HasMorePages = true;
                    return;
                }

                e.HasMorePages = false;
            }
        }
    }
}
