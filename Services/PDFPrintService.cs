using RecordsMaster.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;

namespace RecordsMaster.Services
{
    public class PDFPrintService
    {

        public void PrintLabels(List<RecordItemModel> records, string? printerName = null)
        {
            if (records == null || records.Count == 0)
                throw new ArgumentException("No records provided.", nameof(records));

            // Turn the list item into a string.
            static string MakeSafe(string? value)
            {
                if (string.IsNullOrWhiteSpace(value))
                    return "UNKNOWN";

                char[] invalid = Path.GetInvalidFileNameChars();
                var chars = value.Trim().ToCharArray();
                for (int i = 0; i < chars.Length; i++)
                {
                    if (Array.IndexOf(invalid, chars[i]) >= 0)
                        chars[i] = '_';
                }
                return new string(chars);
            }

            string firstBarcode = MakeSafe(records[0]?.BarCode);
            string lastBarcode = MakeSafe(records[^1]?.BarCode);

            // File name consists of the first and last BarCode values
            string tempPdfPath = Path.Combine(Path.GetTempPath(), $"{firstBarcode}_{lastBarcode}.pdf");

            Console.WriteLine($"PDF Path: {tempPdfPath}");

            // Create PDF with labels
            CreateLabelsPdf(records, tempPdfPath);

            // Send to printer (cross-platform)
            PrintPdfCrossPlatform(tempPdfPath, printerName);
        }

        private void CreateLabelsPdf(List<RecordItemModel> records, string filePath)
        {
            using var document = new PdfDocument();
            var page = document.AddPage();
            page.Width = XUnit.FromInch(8.5);
            page.Height = XUnit.FromInch(11);

            var gfx = XGraphics.FromPdfPage(page);

            // Fonts: small for Case#, large for BarCode and RecordType
            var fontSmall = new XFont("Arial", 12);
            var fontLarge = new XFont("Arial", 24);

            float labelWidth = (float)XUnit.FromInch(4);
            float labelHeight = (float)XUnit.FromInch(1.33);
            float margin = (float)XUnit.FromInch(0.2);

            int labelsPerRow = 2;
            int labelsPerColumn = 7;
            int labelsPerPage = labelsPerRow * labelsPerColumn;

            int recordIndex = 0;

            while (recordIndex < records.Count)
            {
                for (int row = 0; row < labelsPerColumn; row++)
                {
                    for (int col = 0; col < labelsPerRow; col++)
                    {
                        if (recordIndex >= records.Count)
                            break;

                        float x = margin + col * (labelWidth + margin);
                        float y = margin + row * (labelHeight + margin);

                        var record = records[recordIndex++];

                        // Draw label border
                        gfx.DrawRectangle(XPens.Black, x, y, labelWidth, labelHeight);

                        // Text padding inside label
                        double padding = 5;
                        double xText = x + padding;
                        double yText = y + padding + fontSmall.Size; // baseline for first line

                        // Line 1: Case# (small font)
                        gfx.DrawString($"Case#: {record.CIS}", fontSmall, XBrushes.Black, new XPoint(xText, yText));

                        // Line 2: BarCode (large font)
                        yText += fontLarge.Size + 2; // simple spacing
                        gfx.DrawString($"{record.BarCode}", fontLarge, XBrushes.Black, new XPoint(xText, yText));

                        // Line 3: RecordType (large font)
                        yText += fontLarge.Size + 2;
                        gfx.DrawString($"{record.RecordType}", fontLarge, XBrushes.Black, new XPoint(xText, yText));
                    }
                }

                if (recordIndex < records.Count)
                {
                    page = document.AddPage();
                    page.Width = XUnit.FromInch(8.5);
                    page.Height = XUnit.FromInch(11);
                    gfx = XGraphics.FromPdfPage(page);
                }
            }

            document.Save(filePath);
        }

        public (byte[] pdfBytes, string fileName) GenerateLabelsPdfForDownload(List<RecordItemModel> records)
        {
            if (records == null || records.Count == 0)
                throw new ArgumentException("No records provided.", nameof(records));

            static string MakeSafe(string? value)
            {
                if (string.IsNullOrWhiteSpace(value))
                    return "UNKNOWN";

                char[] invalid = Path.GetInvalidFileNameChars();
                var chars = value.Trim().ToCharArray();
                for (int i = 0; i < chars.Length; i++)
                {
                    if (Array.IndexOf(invalid, chars[i]) >= 0)
                        chars[i] = '_';
                }
                var safeString = new string(chars);
                if (string.IsNullOrWhiteSpace(safeString))
                    return "UNKNOWN";

                return safeString;
            }

            string firstBarcode = MakeSafe(records[0]?.BarCode);
            string lastBarcode = MakeSafe(records[^1]?.BarCode);

            string fileName = $"{firstBarcode} - {lastBarcode}.pdf";

            using var document = new PdfDocument();

            int labelsPerRow = 2;
            int labelsPerColumn = 7;

            var fontSmall = new XFont("Arial", 12);
            var fontLarge = new XFont("Arial", 24);

            float labelWidth = (float)XUnit.FromInch(4);
            float labelHeight = (float)XUnit.FromInch(1.33);
            float margin = (float)XUnit.FromInch(0.2);

            int recordIndex = 0;

            while (recordIndex < records.Count)
            {
                var page = document.AddPage();
                page.Width = XUnit.FromInch(8.5);
                page.Height = XUnit.FromInch(11);
                var gfx = XGraphics.FromPdfPage(page);

                for (int row = 0; row < labelsPerColumn; row++)
                {
                    for (int col = 0; col < labelsPerRow; col++)
                    {
                        if (recordIndex >= records.Count)
                            break;

                        float x = margin + col * (labelWidth + margin);
                        float y = margin + row * (labelHeight + margin);

                        var record = records[recordIndex++];

                        gfx.DrawRectangle(XPens.Black, x, y, labelWidth, labelHeight);

                        double padding = 5;
                        double xText = x + padding;
                        double yText = y + padding + fontSmall.Size;

                        gfx.DrawString($"Case#: {record.CIS}", fontSmall, XBrushes.Black, new XPoint(xText, yText));

                        yText += fontLarge.Size + 2;
                        gfx.DrawString($"{record.BarCode}", fontLarge, XBrushes.Black, new XPoint(xText, yText));

                        yText += fontLarge.Size + 2;
                        gfx.DrawString($"{record.RecordType}", fontLarge, XBrushes.Black, new XPoint(xText, yText));
                    }
                }
            }

            using var ms = new MemoryStream();
            document.Save(ms, false);
            return (ms.ToArray(), fileName);
        }

        private void PrintPdfCrossPlatform(string pdfFilePath, string? printerName = null)
        {
            if (!File.Exists(pdfFilePath))
                throw new FileNotFoundException("PDF not found for printing.", pdfFilePath);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string adobeReaderPath = @"C:\Program Files\Adobe\Acrobat Reader DC\Reader\AcroRd32.exe";
                if (File.Exists(adobeReaderPath))
                {
                    string args = $"/t \"{pdfFilePath}\" \"{printerName}\"";
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = adobeReaderPath,
                        Arguments = args,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    });
                }
                else
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = pdfFilePath,
                        Verb = "print",
                        CreateNoWindow = true,
                        UseShellExecute = true
                    });
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                     RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                string lpCommand = "lp";
                string lpArgs = string.IsNullOrEmpty(printerName)
                    ? $"\"{pdfFilePath}\""
                    : $"-d \"{printerName}\" \"{pdfFilePath}\"";

                Process.Start(new ProcessStartInfo
                {
                    FileName = lpCommand,
                    Arguments = lpArgs,
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            else
            {
                throw new PlatformNotSupportedException("Unsupported OS for printing");
            }
        }
    }
}