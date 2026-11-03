using System;

namespace AlaaRep.Spreadsheet.Core.Models
{
    /// <summary>
    /// Represents single cell content and format.
    /// </summary>
    public sealed class CellData
    {
        public object? Value { get; set; }
        public CellFormat Format { get; set; } = CellFormat.Default;

        public CellData() { }

        public CellData(object? value, CellFormat? format = null)
        {
            Value = value;
            Format = format ?? CellFormat.Default;
        }

        public string GetFormattedText()
        {
            return Format.FormatValue(Value);
        }

        public CellData Clone()
        {
            return new CellData(Value, Format);
        }
    }
}
