#nullable disable
using System;
using System.Windows.Input;

namespace ZooscapeRunner.ViewModels
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object?>? _executeWithParam;
        private readonly Action? _executeWithoutParam;
        private readonly Func<object?, bool>? _canExecuteWithParam;
        private readonly Func<bool>? _canExecuteWithoutParam;

        // Constructor for parameterless actions
        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _executeWithoutParam = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecuteWithoutParam = canExecute;
        }

        // Constructor for actions with parameters
        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _executeWithParam = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecuteWithParam = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            if (_canExecuteWithoutParam != null)
                return _canExecuteWithoutParam();
            if (_canExecuteWithParam != null)
                return _canExecuteWithParam(parameter);
            return true;
        }

        public void Execute(object? parameter)
        {
            if (_executeWithoutParam != null)
                _executeWithoutParam();
            else
                _executeWithParam?.Invoke(parameter);
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
