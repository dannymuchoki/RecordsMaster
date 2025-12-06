using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CsvHelper;
using CsvHelper.Configuration;
using RecordsMaster.Models;

// This reads from a csv

namespace RecordsMaster.Utilities
{
    public static class CsvRecordReader
    {
        public static IEnumerable<RecordItemModel> ReadRecordsFromCsv(string filePath)
        {
            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,
                MissingFieldFound = null
            });

            foreach (var record in csv.GetRecords<RecordItemModel>())
            {
                record.ID = Guid.NewGuid(); // Generate GUID for each record
                record.CheckedOutTo = null; 
                yield return record;
            }
        }
    }
}