using System.Windows.Input;

namespace ClipboardNext.App;

/// <summary>A minimal command for the tray icon's click bindings.</summary>
internal sealed partial class RelayCommand(Action execute) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => execute();
}
