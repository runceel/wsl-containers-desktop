using System.ComponentModel;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WslContainersDesktop_App.Navigation;
using WslContainersDesktop_App.ViewModels;
using Windows.ApplicationModel.Resources;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WslContainersDesktop_App;

public sealed partial class MainWindow : Window
{
    private readonly NavigationViewModel _viewModel = new();
    private readonly Dictionary<NavigationViewItem, NavigationPageKey> _navigationItemToPageKey;

    public MainWindow()
    {
        InitializeComponent();

        // Window.Title は DependencyProperty ではないため x:Uid によるローカライズ対象にできない。
        // ResourceLoader からコードビハインドで明示的に設定する。
        Title = new ResourceLoader().GetString("MainWindow.Title");

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.SetIcon("Assets/AppIcon.ico");

        _navigationItemToPageKey = new()
        {
            [NavItemContainers] = NavigationPageKey.Containers,
            [NavItemImages] = NavigationPageKey.Images,
            [NavItemVolumes] = NavigationPageKey.Volumes,
            [NavItemNetworks] = NavigationPageKey.Networks,
            [NavItemSettings] = NavigationPageKey.Settings,
        };

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;

        SyncNavigationViewWithCurrentPage();
    }

    private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
    {
        NavView.IsPaneOpen = !NavView.IsPaneOpen;
    }

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item &&
            _navigationItemToPageKey.TryGetValue(item, out var pageKey))
        {
            _viewModel.NavigateToCommand.Execute(pageKey);
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NavigationViewModel.CurrentPageKey))
        {
            SyncNavigationViewWithCurrentPage();
        }
    }

    /// <summary>
    /// <see cref="NavigationViewModel.CurrentPageKey"/> に合わせて、
    /// <see cref="NavigationView.SelectedItem"/> の選択状態と <see cref="Frame"/> の表示ページを同期する。
    /// </summary>
    private void SyncNavigationViewWithCurrentPage()
    {
        var currentPageKey = _viewModel.CurrentPageKey;

        foreach (var (item, pageKey) in _navigationItemToPageKey)
        {
            if (pageKey == currentPageKey)
            {
                NavView.SelectedItem = item;
                break;
            }
        }

        NavFrame.Navigate(NavigationPageRegistry.GetPageType(currentPageKey));

        // トップレベルのNavigationView切替はタブ切り替えに相当し、Backスタックの概念を持たない。
        // Backボタンを無効化しても、蓄積したBackスタックが残っているとCurrentPageKeyと
        // 実際の表示ページが将来ずれる可能性があるため、遷移のたびに明示的にクリアする。
        NavFrame.BackStack.Clear();
    }
}
