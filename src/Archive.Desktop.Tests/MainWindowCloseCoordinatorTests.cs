namespace Archive.Desktop.Tests;

public class MainWindowCloseCoordinatorTests
{
    [Fact]
    public void GetCloseAction_DefaultsToHideToTray()
    {
        var coordinator = new MainWindowCloseCoordinator();

        var action = coordinator.GetCloseAction();

        Assert.Equal(MainWindowCloseAction.HideToTray, action);
    }

    [Fact]
    public void GetCloseAction_ReturnsCloseApplication_AfterShutdownRequested()
    {
        var coordinator = new MainWindowCloseCoordinator();
        coordinator.RequestApplicationShutdown();

        var action = coordinator.GetCloseAction();

        Assert.Equal(MainWindowCloseAction.CloseApplication, action);
    }
}
