using System;
using System.Windows.Input;

namespace Annotation.ViewModels.Commands
{
    public class DelegateCommand : System.Windows.Input.ICommand
    {  
        public Action<object> on_execute;
        public Func<object, bool> can_execute; 

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }

        public DelegateCommand(Action<object> Execute, Func<object, bool> CanExecute)
        {
            on_execute = Execute;
            can_execute = CanExecute;
        }
         
        public bool CanExecute(object parameter)
        {
            if (can_execute != null)
                return can_execute(parameter);

            return true;
        }

        public void Execute(object parameter)
        {
            on_execute(parameter);
            RaiseCanExecuteChanged();
        }
    }

    public class DelegateCommand<T> : System.Windows.Input.ICommand
    {  
        public Action<T> on_execute;
        public Func<T, bool> can_execute;

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }

        public DelegateCommand(Action<T> Execute, Func<T, bool> CanExecute)
        {
            on_execute = Execute;
            can_execute = CanExecute;
        }

        public bool CanExecute(object parameter)
        {
            if (can_execute != null)
                return can_execute((T)parameter);

            return true;
        }

        public void Execute(object parameter)
        {
            on_execute((T)parameter);
            RaiseCanExecuteChanged();
        }
    }
}
