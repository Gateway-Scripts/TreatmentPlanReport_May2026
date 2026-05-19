using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace TreatmentPlanReport.Helpers
{
    // RelayCommand class to handle command binding in ViewModel.
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;

        // Event that gets fired when CanExecute changes.
        public event EventHandler CanExecuteChanged;

        // Constructor for commands without a condition.
        public RelayCommand(Action<object> execute) : this(execute, null) { }

        // Constructor for commands with a condition.
        public RelayCommand(Action<object> execute, Func<object, bool> canExecute)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        // Determines if the command can be executed.
        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute(parameter);
        }

        // Executes the command action.
        public void Execute(object parameter)
        {
            _execute(parameter);
        }
        // Raises the CanExecuteChanged event to notify the UI of state changes.
        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
