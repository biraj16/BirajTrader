using System;
using System.Windows.Input;

namespace TradingConsole.Wpf.ViewModels
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        // --- FIX: Parameter is now nullable to match ICommand interface ---
        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);

        // --- FIX: Parameter is now nullable to match ICommand interface ---
        public void Execute(object? parameter) => _execute(parameter);

        // --- FIX: Event is now nullable to match ICommand interface ---
        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
