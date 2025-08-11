using System;
using System.Windows.Input;

namespace MaterRevitAddin
{
    public class RelayCommand : ICommand
    {
        private readonly Action _exec;
        private readonly Func<bool>? _can;
        public event EventHandler? CanExecuteChanged;
        public RelayCommand(Action exec, Func<bool>? can = null) { _exec = exec; _can = can; }
        public bool CanExecute(object? parameter) => _can?.Invoke() ?? true;
        public void Execute(object? parameter) => _exec();
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
