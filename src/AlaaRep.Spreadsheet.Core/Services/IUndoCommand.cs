using System;

namespace AlaaRep.Spreadsheet.Core.Services
{
    public interface IUndoCommand
    {
        string Description { get; }
        void Execute();
        void Undo();
    }
}
