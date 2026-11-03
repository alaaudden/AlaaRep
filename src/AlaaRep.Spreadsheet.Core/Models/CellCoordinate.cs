using System;
using System.Text.RegularExpressions;

namespace AlaaRep.Spreadsheet.Core.Models
{
    /// <summary>
    /// Represents a zero-indexed row and column coordinate in the spreadsheet.
    /// Supports standard Excel coordinate string conversions (e.g. A1, B10, AA25).
    /// </summary>
    public readonly struct CellCoordinate : IEquatable<CellCoordinate>, IComparable<CellCoordinate>
    {
        public int Row { get; }
        public int Column { get; }

        public CellCoordinate(int row, int column)
        {
            if (row < 0) throw new ArgumentOutOfRangeException(nameof(row), "Row must be non-negative.");
            if (column < 0) throw new ArgumentOutOfRangeException(nameof(column), "Column must be non-negative.");

            Row = row;
            Column = column;
        }

        public bool Equals(CellCoordinate other) => Row == other.Row && Column == other.Column;

        public override bool Equals(object? obj) => obj is CellCoordinate other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Row, Column);

        public int CompareTo(CellCoordinate other)
        {
            int rowCompare = Row.CompareTo(other.Row);
            if (rowCompare != 0) return rowCompare;
            return Column.CompareTo(other.Column);
        }

        public static bool operator ==(CellCoordinate left, CellCoordinate right) => left.Equals(right);
        public static bool operator !=(CellCoordinate left, CellCoordinate right) => !left.Equals(right);

        public override string ToString() => Format(Row, Column);

        /// <summary>
        /// Converts 0-indexed row and column into Excel style reference (e.g. 0, 0 -> A1).
        /// </summary>
        public static string Format(int row, int column)
        {
            return $"{ColumnIndexToLetters(column)}{row + 1}";
        }

        /// <summary>
        /// Parses an Excel style reference (e.g. "A1", "C25", "AA100") into a CellCoordinate.
        /// </summary>
        public static bool TryParse(string text, out CellCoordinate coordinate)
        {
            coordinate = default;
            if (string.IsNullOrWhiteSpace(text)) return false;

            text = text.Trim().ToUpperInvariant();
            var match = Regex.Match(text, @"^([A-Z]+)([0-9]+)$");
            if (!match.Success) return false;

            string letters = match.Groups[1].Value;
            string digits = match.Groups[2].Value;

            if (!int.TryParse(digits, out int rowNumber) || rowNumber < 1) return false;
            int colIndex = LettersToColumnIndex(letters);
            if (colIndex < 0) return false;

            coordinate = new CellCoordinate(rowNumber - 1, colIndex);
            return true;
        }

        public static CellCoordinate Parse(string text)
        {
            if (TryParse(text, out var coord)) return coord;
            throw new FormatException($"Invalid cell reference '{text}'. Expected format like 'A1'.");
        }

        public static string ColumnIndexToLetters(int columnIndex)
        {
            if (columnIndex < 0) throw new ArgumentOutOfRangeException(nameof(columnIndex));

            string letters = string.Empty;
            int current = columnIndex + 1;
            while (current > 0)
            {
                int rem = (current - 1) % 26;
                letters = (char)('A' + rem) + letters;
                current = (current - 1) / 26;
            }
            return letters;
        }

        public static int LettersToColumnIndex(string letters)
        {
            if (string.IsNullOrWhiteSpace(letters)) return -1;
            int sum = 0;
            foreach (char c in letters.ToUpperInvariant())
            {
                if (c < 'A' || c > 'Z') return -1;
                sum = sum * 26 + (c - 'A' + 1);
            }
            return sum - 1;
        }
    }
}
