namespace Archive.Desktop;

public enum MainWindowCloseAction
{
    HideToTray,
    CloseApplication
}

public sealed class MainWindowCloseCoordinator
{
    private bool _applicationShutdownRequested;

    public MainWindowCloseAction GetCloseAction()
    {
        return _applicationShutdownRequested
            ? MainWindowCloseAction.CloseApplication
            : MainWindowCloseAction.HideToTray;
    }

    public void RequestApplicationShutdown()
    {
        _applicationShutdownRequested = true;
    }
}