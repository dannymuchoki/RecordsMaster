using RecordsMaster.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
#if NET10_0_OR_GREATER
using PdfSharp.Pdf;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
#else
using PdfSharpCore.Pdf;
using PdfSharpCore.Drawing;
#endif

namespace RecordsMaster.Services
{
    public class PDFPrintService
    {
#if NET10_0_OR_GREATER
        static PDFPrintService()
        {
            GlobalFontSettings.FontResolver = new SystemFontResolver();
        }

        private sealed class SystemFontResolver : IFontResolver
        {
            private static readonly string[] FontDirectories = BuildFontDirectories();

            private static string[] BuildFontDirectories()
            {
                var dirs = new List<string>
                {
                    // App-local fonts/ folder — works everywhere including IIS restricted accounts
                    Path.Combine(AppContext.BaseDirectory, "fonts")
                };

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // SpecialFolder.Fonts resolves correctly for both interactive and IIS app pool accounts
                    var winFonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
                    if (!string.IsNullOrEmpty(winFonts))
                        dirs.Add(winFonts);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    dirs.Add("/System/Library/Fonts/Supplemental");
                    dirs.Add("/Library/Fonts");
                    dirs.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library/Fonts"));
                }
                else
                {
                    dirs.Add("/usr/share/fonts");
                    dirs.Add("/usr/local/share/fonts");
                }

                return dirs.ToArray();
            }

            public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
            {
                string[] candidates;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Windows font files use compact names: arialbd.ttf, ariali.ttf, arialbi.ttf
                    string winBase = familyName.Replace(" ", "").ToLowerInvariant();
                    string winSuffix = (isBold && isItalic) ? "bi" : isBold ? "bd" : isItalic ? "i" : "";
                    candidates =
                    [
                        winBase + winSuffix,
                        familyName + (isBold ? " Bold" : "") + (isItalic ? " Italic" : ""),
                        familyName
                    ];
                }
                else
                {
                    string suffix = (isBold ? " Bold" : "") + (isItalic ? " Italic" : "");
                    candidates = [familyName + suffix, familyName + suffix.Replace(" ", ""), familyName];
                }

                foreach (var dir in FontDirectories)
                {
                    if (!Directory.Exists(dir)) continue;
                    foreach (var name in candidates)
                    {
                        foreach (var ext in new[] { ".ttf", ".otf" })
                        {
                            var path = Path.Combine(dir, name + ext);
                            if (File.Exists(path))
                                return new FontResolverInfo(path);
                        }
                    }
                }
                return null;
            }

            public byte[]? GetFont(string faceName) =>
                File.Exists(faceName) ? File.ReadAllBytes(faceName) : null;
        }
#endif

        // The labels are formatted to the Avery 5162 standard with two columns of seven labels.
        public void PrintLabels(List<RecordItemModel> records, string? printerName = null)
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
                return new string(chars);
            }

            string firstBarcode = MakeSafe(records[0]?.BarCode);
            string lastBarcode = MakeSafe(records[^1]?.BarCode);

            string tempPdfPath = Path.Combine(Path.GetTempPath(), $"{firstBarcode}_{lastBarcode}.pdf");

            Console.WriteLine($"PDF Path: {tempPdfPath}");

            CreateLabelsPdf(records, tempPdfPath);
            PrintPdfCrossPlatform(tempPdfPath, printerName);
        }

        private void CreateLabelsPdf(List<RecordItemModel> records, string filePath)
        {
            using var document = new PdfDocument();
            var page = document.AddPage();
            page.Width = XUnit.FromInch(8.5);
            page.Height = XUnit.FromInch(11);

            var gfx = XGraphics.FromPdfPage(page);

            var fontSmall = new XFont("Arial", 12);
            var fontLarge = new XFont("Arial", 24);

            double labelWidth = XUnit.FromInch(4).Point;
            double labelHeight = XUnit.FromInch(1.33).Point;
            double margin = XUnit.FromInch(0.2).Point;
            double topMargin = XUnit.FromCentimeter(2.1).Point;
            double bottomMargin = XUnit.FromCentimeter(2.1).Point;

            int labelsPerRow = 2;
            int labelsPerColumn = 7;

            int recordIndex = 0;

            while (recordIndex < records.Count)
            {
                double pageHeight = page.Height.Point;

                for (int row = 0; row < labelsPerColumn; row++)
                {
                    double y = topMargin + row * labelHeight;
                    if (y + labelHeight > pageHeight - bottomMargin)
                        break;

                    for (int col = 0; col < labelsPerRow; col++)
                    {
                        if (recordIndex >= records.Count)
                            break;

                        double x = margin + col * (labelWidth + margin);
                        var record = records[recordIndex++];

                        gfx.DrawRectangle(XPens.Black, x, y, labelWidth, labelHeight);

                        double padding = 5;
                        double contentTopOffset = XUnit.FromCentimeter(0.4).Point; // nudge text down within the pre-printed label
                        double xText = x + padding;
                        double yText = y + padding + contentTopOffset + fontSmall.Size;

                        gfx.DrawString($"Case#: {record.CIS}", fontSmall, XBrushes.Black, new XPoint(xText, yText));

                        yText += fontLarge.Size + 2;
                        gfx.DrawString($"{record.BarCode}", fontLarge, XBrushes.Black, new XPoint(xText, yText));

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
                return string.IsNullOrWhiteSpace(safeString) ? "UNKNOWN" : safeString;
            }

            string firstBarcode = MakeSafe(records[0]?.BarCode);
            string lastBarcode = MakeSafe(records[^1]?.BarCode);
            string fileName = $"{firstBarcode} - {lastBarcode}.pdf";

            using var document = new PdfDocument();

            var fontSmall = new XFont("Arial", 12);
            var fontLarge = new XFont("Arial", 24);

            double labelWidth = XUnit.FromInch(4).Point;
            double labelHeight = XUnit.FromInch(1.33).Point;
            double margin = XUnit.FromInch(0.2).Point;
            double topMargin = XUnit.FromCentimeter(2.1).Point;
            double bottomMargin = XUnit.FromCentimeter(2.1).Point;

            int labelsPerRow = 2;
            int labelsPerColumn = 7;
            int recordIndex = 0;

            while (recordIndex < records.Count)
            {
                var page = document.AddPage();
                page.Width = XUnit.FromInch(8.5);
                page.Height = XUnit.FromInch(11);
                var gfx = XGraphics.FromPdfPage(page);

                double pageHeight = page.Height.Point;

                for (int row = 0; row < labelsPerColumn; row++)
                {
                    double y = topMargin + row * labelHeight;
                    if (y + labelHeight > pageHeight - bottomMargin)
                        break;

                    for (int col = 0; col < labelsPerRow; col++)
                    {
                        if (recordIndex >= records.Count)
                            break;

                        double x = margin + col * (labelWidth + margin);
                        var record = records[recordIndex++];

                        gfx.DrawRectangle(XPens.Black, x, y, labelWidth, labelHeight);

                        double padding = 5;
                        double contentTopOffset = XUnit.FromCentimeter(0.4).Point; // nudge text down within the pre-printed label
                        double xText = x + padding;
                        double yText = y + padding + contentTopOffset + fontSmall.Size;

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
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = adobeReaderPath,
                        Arguments = $"/t \"{pdfFilePath}\" \"{printerName}\"",
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
                string lpArgs = string.IsNullOrEmpty(printerName)
                    ? $"\"{pdfFilePath}\""
                    : $"-d \"{printerName}\" \"{pdfFilePath}\"";

                Process.Start(new ProcessStartInfo
                {
                    FileName = "lp",
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
