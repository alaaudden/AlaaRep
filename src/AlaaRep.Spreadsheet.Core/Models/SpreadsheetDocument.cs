using System;
using System.Collections.ObjectModel;

namespace AlaaRep.Spreadsheet.Core.Models
{
    /// <summary>
    /// Represents an Excel workbook document holding multiple sheets.
    /// </summary>
    public class SpreadsheetDocument
    {
        public ObservableCollection<SheetDataModel> Sheets { get; } = new();

        private SheetDataModel? _activeSheet;
        public SheetDataModel ActiveSheet
        {
            get => _activeSheet ?? (_activeSheet = Sheets.Count > 0 ? Sheets[0] : CreateDefaultSheet());
            set
            {
                if (_activeSheet != value)
                {
                    _activeSheet = value;
                    ActiveSheetChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler? ActiveSheetChanged;

        public SpreadsheetDocument()
        {
            CreateDefaultSheet();
        }

        private SheetDataModel CreateDefaultSheet()
        {
            var sheet = new SheetDataModel("Sheet1");
            Sheets.Add(sheet);
            _activeSheet = sheet;
            return sheet;
        }

        public SheetDataModel AddSheet(string? name = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                int num = Sheets.Count + 1;
                while (true)
                {
                    string candidate = $"Sheet{num}";
                    bool exists = false;
                    foreach (var s in Sheets)
                    {
                        if (string.Equals(s.Name, candidate, StringComparison.OrdinalIgnoreCase))
                        {
                            exists = true;
                            break;
                        }
                    }
                    if (!exists)
                    {
                        name = candidate;
                        break;
                    }
                    num++;
                }
            }

            var newSheet = new SheetDataModel(name);
            Sheets.Add(newSheet);
            ActiveSheet = newSheet;
            return newSheet;
        }

        public bool RemoveSheet(SheetDataModel sheet)
        {
            if (Sheets.Count <= 1) return false; // Keep at least one sheet

            int index = Sheets.IndexOf(sheet);
            if (index >= 0)
            {
                Sheets.RemoveAt(index);
                if (ActiveSheet == sheet)
                {
                    int nextIndex = Math.Min(index, Sheets.Count - 1);
                    ActiveSheet = Sheets[nextIndex];
                }
                return true;
            }
            return false;
        }
    }
}
