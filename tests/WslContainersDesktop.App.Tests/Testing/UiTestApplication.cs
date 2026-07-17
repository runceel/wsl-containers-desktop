using Microsoft.UI.Xaml;

namespace WslContainersDesktop_App_Tests.Testing;

/// <summary>
/// Provides the XAML application used by WinUI tests.
/// </summary>
public sealed class UiTestApplication : Application
{
    private readonly Window _lifetimeWindow;

    /// <summary>
    /// Initializes the test application and keeps its shared UI dispatcher alive.
    /// </summary>
    public UiTestApplication()
    {
        _lifetimeWindow = new Window();
        _lifetimeWindow.Activate();
        _lifetimeWindow.AppWindow.Hide();
    }
}
