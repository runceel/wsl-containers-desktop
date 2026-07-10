using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Application.Services;
using WslContainersDesktop.Infrastructure.Cli;
using WslContainersDesktop.Infrastructure.Clients;
using WslContainersDesktop.Infrastructure.Settings;
using WslContainersDesktop.Infrastructure.Wsl;
using WslContainersDesktop_App.ViewModels;
using WslContainersDesktop_App.Windows;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WslContainersDesktop_App;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _window;

    /// <summary>
    /// Composition Root（<see href="../../docs/adr/0010-adopt-di-container-for-presentation.md">ADR-0010</see>）
    /// が構築したサービスプロバイダー。<c>Frame.Navigate(Type)</c>ベースの遷移ではページが
    /// パラメーターレスコンストラクタを要求されるため、ページ側からこのプロパティ経由で
    /// 依存を解決する。
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();
        Services = ConfigureServices();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IWslcCliRunner, WslcCliRunner>();
        services.AddSingleton<IContainerRuntimeClient, WslcCliContainerRuntimeClient>();
        services.AddSingleton<IContainerManagementService, ContainerManagementService>();
        services.AddSingleton<IImageManagementService, ImageManagementService>();
        services.AddSingleton<IVolumeManagementService, VolumeManagementService>();
        services.AddSingleton<INetworkManagementService, NetworkManagementService>();
        services.AddSingleton<IDashboardService, DashboardService>();
        services.AddSingleton<IUiDispatcher>(_ => new DispatcherQueueUiDispatcher(DispatcherQueue.GetForCurrentThread()));

        // 設定画面（Issue #7）。WSL環境検出と .wslconfig 編集の低レベルseamと、
        // 要件ポリシーを所有するSettingsServiceを登録する。
        services.AddSingleton<IWslCommandRunner, WslCommandRunner>();
        services.AddSingleton<IWslcExecutableProbe, WslcExecutableProbe>();
        services.AddSingleton<IWslConfigFileAccessor, WslConfigFileAccessor>();
        services.AddSingleton<IWslEnvironmentProbe, WslEnvironmentProbe>();
        services.AddSingleton<IWslResourceLimitsStore, WslConfigResourceLimitsStore>();
        services.AddSingleton<ISettingsService, SettingsService>();

        // トップレベルページのViewModelはNavigationViewModelと同様、アプリケーション
        // ライフタイムで1インスタンスとする。
        services.AddSingleton<NavigationViewModel>();
        services.AddSingleton<DashboardViewModel>();
        services.AddSingleton<ContainersViewModel>();
        services.AddSingleton<ImagesViewModel>();
        services.AddSingleton<VolumesViewModel>();
        services.AddSingleton<NetworksViewModel>();
        services.AddSingleton<SettingsViewModel>();

        // Logs/Shellパネルのポップアウトウィンドウは、ContainersPageがNavigateのたびに
        // 作り直されても状態を維持できるよう、アプリケーションライフタイムのSingletonとする。
        services.AddSingleton<ContainerAuxiliaryWindowManager>();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
