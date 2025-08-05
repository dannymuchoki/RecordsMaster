using RecordsMaster.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;

namespace RecordsMaster.Services
{
    public class LabelPrintService
    {
        private int _recordIndex; // track page count

        public void PrintLabels(List<RecordItemModel> records)
        {
            _recordIndex = 0;

            PrintDocument printDoc = new PrintDocument();
            printDoc.PrintPage += (sender, e) => PrintPageHandler(e, records);
            printDoc.Print(); 

            
        }

        private void PrintPageHandler(PrintPageEventArgs e, List<RecordItemModel> records)
        {
            float labelWidth = 4 * 100;       // 4 inches in hundredths of an inch
            float labelHeight = 1.33f * 100;  // 1.33 inches
            float margin = 0.2f * 100;        // 0.2 inches

            int labelsPerRow = 2;
            int labelsPerColumn = 7;
            int labelsPerPage = labelsPerRow * labelsPerColumn;

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

                    string labelText = $"CIS: {record.CIS}\nBarCode: {record.BarCode}\nRecordType: {record.RecordType}";
                    e.Graphics.DrawString(labelText, new Font("Arial", 12), Brushes.Black, new RectangleF(x + 5, y + 5, labelWidth - 10, labelHeight - 10));
                }
            }

            _recordIndex = endIndex;

            // Indicate whether there are more pages to print
            e.HasMorePages = _recordIndex < records.Count;
        }
    }
}
