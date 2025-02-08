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
        Map(m => m.BoxNumber).Index(3);
        Map(m => m.Digitized).Index(4).TypeConverter<BooleanTypeConverter>();
        Map(m => m.ClosingDate).Index(5);
        Map(m => m.DestroyDate).Index(6);
        //Map(m => m.CheckedOut).Index(7);
        Map(m => m.CheckedOut).Index(7).TypeConverter<BooleanTypeConverter>();
        Map(m => m.CheckedOutBy).Index(8);
        
    }
}

// Custom converter to handle boolean values
public class BooleanTypeConverter : BooleanConverter
{
    public override object ConvertFromString(string text, IReaderRow row, MemberMapData memberMapData)
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

        return base.ConvertFromString(text, row, memberMapData);
    }
}
