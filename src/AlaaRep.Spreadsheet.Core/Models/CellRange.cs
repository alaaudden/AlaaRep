using System;

namespace AlaaRep.Spreadsheet.Core.Models
{
    /// <summary>
    /// Represents a 2D rectangular range of cells.
    /// </summary>
    public readonly struct CellRange : IEquatable<CellRange>
    {
        public int StartRow { get; }
        public int StartColumn { get; }
        public int EndRow { get; }
        public int EndColumn { get; }

        public int Top => Math.Min(StartRow, EndRow);
        public int Bottom => Math.Max(StartRow, EndRow);
        public int Left => Math.Min(StartColumn, EndColumn);
        public int Right => Math.Max(StartColumn, EndColumn);

        public int RowCount => Bottom - Top + 1;
        public int ColumnCount => Right - Left + 1;
        public long TotalCells => (long)RowCount * ColumnCount;

        public CellRange(int startRow, int startColumn, int endRow, int endColumn)
        {
            if (startRow < 0) throw new ArgumentOutOfRangeException(nameof(startRow));
            if (startColumn < 0) throw new ArgumentOutOfRangeException(nameof(startColumn));
            if (endRow < 0) throw new ArgumentOutOfRangeException(nameof(endRow));
            if (endColumn < 0) throw new ArgumentOutOfRangeException(nameof(endColumn));

            StartRow = startRow;
            StartColumn = startColumn;
            EndRow = endRow;
            EndColumn = endColumn;
        }

        public CellRange(CellCoordinate from, CellCoordinate to)
            : this(from.Row, from.Column, to.Row, to.Column)
        {
        }

        public CellRange(CellCoordinate singleCell)
            : this(singleCell.Row, singleCell.Column, singleCell.Row, singleCell.Column)
        {
        }

        public bool Contains(int row, int col)
        {
            return row >= Top && row <= Bottom && col >= Left && col <= Right;
        }

        public bool Contains(CellCoordinate coordinate)
        {
            return Contains(coordinate.Row, coordinate.Column);
        }

        public bool Intersects(CellRange other)
        {
            return !(other.Left > Right || other.Right < Left || other.Top > Bottom || other.Bottom < Top);
        }

        public bool Equals(CellRange other)
        {
            return Top == other.Top && Bottom == other.Bottom && Left == other.Left && Right == other.Right;
        }

        public override bool Equals(object? obj) => obj is CellRange other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Top, Bottom, Left, Right);

        public static bool operator ==(CellRange left, CellRange right) => left.Equals(right);
        public static bool operator !=(CellRange left, CellRange right) => !left.Equals(right);

        public override string ToString()
        {
            string start = CellCoordinate.Format(Top, Left);
            if (Top == Bottom && Left == Right)
                return start;
            string end = CellCoordinate.Format(Bottom, Right);
            return $"{start}:{end}";
        }

        public static CellRange Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("Cell range string cannot be empty", nameof(text));

            string[] parts = text.Split(':');
            if (parts.Length == 1)
            {
                var coord = CellCoordinate.Parse(parts[0]);
                return new CellRange(coord);
            }
            if (parts.Length == 2)
            {
                var coord1 = CellCoordinate.Parse(parts[0]);
                var coord2 = CellCoordinate.Parse(parts[1]);
                return new CellRange(coord1, coord2);
            }

            throw new FormatException($"Invalid cell range format: {text}");
        }
    }
}
