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

            csv.Context.RegisterClassMap<CsvHelperMap>();

            csv.Read();
            csv.ReadHeader();

            while (csv.Read())
            {
                var record = csv.GetRecord<RecordItemModel>();
                record.ID = Guid.NewGuid();
                // Username/email is in row 11 of the csv 
                record.CheckedOutToName = csv.TryGetField<string>(11, out var checkedOutToName) ? checkedOutToName : null;
                yield return record;
            }
        }
    }
}