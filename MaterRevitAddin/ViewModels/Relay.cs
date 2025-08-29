using System;
using System.Windows.Input;

namespace Mater2026.ViewModels
{
    public class RelayCommand(Action exec, Func<bool>? can = null) : ICommand
    {
        private readonly Action _exec = exec;
        private readonly Func<bool>? _can = can;

        public bool CanExecute(object? p) => _can?.Invoke() ?? true;
        public void Execute(object? p) => _exec();
        public event EventHandler? CanExecuteChanged;
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    public class RelayCommand<T>(Action<T> exec, Func<T, bool>? can = null) : ICommand
    {
        private readonly Action<T> _exec = exec;
        private readonly Func<T, bool>? _can = can;

        public bool CanExecute(object? p) => _can == null || (p is T t && _can(t));
        public void Execute(object? p) { if (p is T t) _exec(t); }
        public event EventHandler? CanExecuteChanged;
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
