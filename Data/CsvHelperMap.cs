using CsvHelper.Configuration;
using RecordsMaster.Models;
using RecordsMaster.Data;
using CsvHelper;
using CsvHelper.TypeConversion;

public sealed class CsvHelperMap : ClassMap<RecordItemModel>
{
    public CsvHelperMap()
    {
        // Ignore the ID since your CSV does not have an ID header.
        Map(m => m.ID).Ignore();
        Map(m => m.CIS).Index(0);
        Map(m => m.BarCode).Index(1);
        Map(m => m.RecordType).Index(2);
        Map(m => m.Location).Index(3);
        Map(m => m.BoxNumber).Index(4).TypeConverter<NullableInt32Converter>();
        Map(m => m.Digitized).Index(5).TypeConverter<BooleanTypeConverter>();
        Map(m => m.ClosingDate).Index(6);
        Map(m => m.DestroyDate).Index(7);
        Map(m => m.CheckedOut).Index(8).TypeConverter<BooleanTypeConverter>();
        Map(m => m.CheckedOutTo).Ignore();
        
    }
}

// Handles non-numeric and empty values for int? fields, returning null instead of throwing.
public class NullableInt32Converter : Int32Converter
{
    public override object? ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (int.TryParse(text.Trim(), out var result))
            return result;

        return null;
    }
}

// Custom converter to handle boolean values
public class BooleanTypeConverter : BooleanConverter
{
    public override object ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        text = text.Trim().ToUpperInvariant();

        if (text == "TRUE" || text == "YES" || text == "1")
        {
            return true;
        }

        if (text == "FALSE" || text == "NO" || text == "0")
        {
            return false;
        }

        var baseResult = base.ConvertFromString(text, row, memberMapData);
        return baseResult ?? false;
    }
}
