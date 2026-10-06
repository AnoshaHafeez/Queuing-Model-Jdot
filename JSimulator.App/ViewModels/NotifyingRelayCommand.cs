using System.Windows.Input;

namespace JSimulator.App.ViewModels;

/// <summary>
/// ICommand for async operations (like running a simulation). Automatically
/// disables itself while the underlying task is executing so a bound button
/// can't be clicked twice, and raises CanExecuteChanged explicitly rather
/// than relying on WPF's global command manager requery.
/// </summary>
public class NotifyingRelayCommand : ICommand
{
    private readonly Func<Task> _executeAsync;
    private readonly Func<bool>? _canExecute;
    private bool _isExecuting;

    public NotifyingRelayCommand(Func<Task> executeAsync, Func<bool>? canExecute = null)
    {
        _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !_isExecuting && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        _isExecuting = true;
        NotifyCanExecuteChanged();
        try
        {
            await _executeAsync();
        }
        finally
        {
            _isExecuting = false;
            NotifyCanExecuteChanged();
        }
    }

    public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
