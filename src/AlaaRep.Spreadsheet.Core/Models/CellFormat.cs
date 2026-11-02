using System;

namespace AlaaRep.Spreadsheet.Core.Models
{
    public enum CellHorizontalAlignment
    {
        Left,
        Center,
        Right
    }

    public enum CellVerticalAlignment
    {
        Top,
        Center,
        Bottom
    }

    public enum NumberFormatType
    {
        General,
        Number,
        Currency,
        Percentage,
        ShortDate,
        Text
    }

    /// <summary>
    /// Immutable record holding cell formatting options.
    /// Can be shared across identical formatted cells to minimize memory footprint.
    /// </summary>
    public sealed record CellFormat
    {
        public static readonly CellFormat Default = new CellFormat();

        public string FontFamily { get; init; } = "Segoe UI";
        public double FontSize { get; init; } = 12.0;
        public bool IsBold { get; init; } = false;
        public bool IsItalic { get; init; } = false;
        public bool IsUnderline { get; init; } = false;
        public bool IsStrikethrough { get; init; } = false;
        public string? ForegroundColorHex { get; init; } = "#FF000000"; // Black
        public string? BackgroundColorHex { get; init; } = null;        // Transparent / Default
        public CellHorizontalAlignment HorizontalAlignment { get; init; } = CellHorizontalAlignment.Left;
        public CellVerticalAlignment VerticalAlignment { get; init; } = CellVerticalAlignment.Center;
        public NumberFormatType FormatType { get; init; } = NumberFormatType.General;
        public string? CustomFormatString { get; init; } = null;
        public bool WrapText { get; init; } = false;

        public string FormatValue(object? rawValue)
        {
            if (rawValue == null) return string.Empty;

            if (!string.IsNullOrEmpty(CustomFormatString))
            {
                try
                {
                    return string.Format("{0:" + CustomFormatString + "}", rawValue);
                }
                catch
                {
                    return rawValue.ToString() ?? string.Empty;
                }
            }

            switch (FormatType)
            {
                case NumberFormatType.Number:
                    if (double.TryParse(rawValue.ToString(), out double numVal))
                        return numVal.ToString("N2");
                    break;
                case NumberFormatType.Currency:
                    if (double.TryParse(rawValue.ToString(), out double curVal))
                        return curVal.ToString("C2");
                    break;
                case NumberFormatType.Percentage:
                    if (double.TryParse(rawValue.ToString(), out double pctVal))
                        return (pctVal * 100.0).ToString("0.00") + "%";
                    break;
                case NumberFormatType.ShortDate:
                    if (DateTime.TryParse(rawValue.ToString(), out DateTime dateVal))
                        return dateVal.ToString("yyyy-MM-dd");
                    break;
                case NumberFormatType.General:
                case NumberFormatType.Text:
                default:
                    break;
            }

            return rawValue.ToString() ?? string.Empty;
        }
    }
}
