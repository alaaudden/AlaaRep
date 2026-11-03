using System;
using System.Collections.Generic;

namespace AlaaRep.Spreadsheet.Core.Services
{
    public class UndoRedoManager
    {
        private readonly Stack<IUndoCommand> _undoStack = new();
        private readonly Stack<IUndoCommand> _redoStack = new();

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public event EventHandler? StateChanged;

        public void ExecuteCommand(IUndoCommand command)
        {
            command.Execute();
            _undoStack.Push(command);
            _redoStack.Clear();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void RegisterExecutedCommand(IUndoCommand command)
        {
            _undoStack.Push(command);
            _redoStack.Clear();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public bool Undo()
        {
            if (_undoStack.Count == 0) return false;

            var command = _undoStack.Pop();
            command.Undo();
            _redoStack.Push(command);
            StateChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public bool Redo()
        {
            if (_redoStack.Count == 0) return false;

            var command = _redoStack.Pop();
            command.Execute();
            _undoStack.Push(command);
            StateChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
