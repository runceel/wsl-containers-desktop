namespace WslContainersDesktop_App_Tests.Pages;

[TestClass]
public sealed class ContainersPageSourceTests
{
    [TestMethod]
    public void ContainersPage_StateColumnBindsDisplayStateOneWay()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\ContainersPage.xaml");

        // Act
        // No-op: the test validates the source binding required by the intermediate-state display feature.

        // Assert
        StringAssert.Contains(
            sourceText,
            "Text=\"{x:Bind DisplayState, Mode=OneWay, Converter={StaticResource StateToDisplayTextConverter}}\"",
            "State列は途中状態（Stopping等）を反映するため DisplayState を Mode=OneWay でバインドする必要がある。");
    }

    [TestMethod]
    public void ContainersPage_ListViewItemsUseSharedTableStyle()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\ContainersPage.xaml");

        // Assert
        StringAssert.Contains(sourceText, "ItemContainerStyle=\"{StaticResource TableListViewItemStyle}\"");
        StringAssert.Contains(sourceText, "<Grid Padding=\"12,8\" ColumnSpacing=\"12\" HorizontalAlignment=\"Stretch\">");
    }

    [TestMethod]
    public void ContainersPage_LogPanelHeader_HasOpenLogsWindowButton()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\ContainersPage.xaml");

        // Assert: ログパネルを大きな個別ウィンドウで開くボタンが存在し、コードビハインドの
        // Clickハンドラに配線されていること。
        StringAssert.Contains(sourceText, "x:Name=\"BtnOpenLogsWindow\"");
        StringAssert.Contains(sourceText, "Click=\"BtnOpenLogsWindow_Click\"");
    }

    [TestMethod]
    public void ContainersPage_ShellPanelHeader_HasOpenShellWindowButton()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\ContainersPage.xaml");

        // Assert: シェルパネルを大きな個別ウィンドウで開くボタンが存在し、コードビハインドの
        // Clickハンドラに配線されていること。
        StringAssert.Contains(sourceText, "x:Name=\"BtnOpenShellWindow\"");
        StringAssert.Contains(sourceText, "Click=\"BtnOpenShellWindow_Click\"");
    }

    [TestMethod]
    public void ContainersPageCodeBehind_OpenLogsWindowClickHandler_DelegatesToAuxiliaryWindowManager()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\ContainersPage.xaml.cs");

        // Assert: ボタンが存在するだけでなく、実際にContainerAuxiliaryWindowManager経由で
        // ログウィンドウを開く呼び出しへ配線されていること(rubber-duckレビュー指摘:
        // ボタンの存在確認だけでは押下時に何もしない実装や誤配線を見逃す)。
        StringAssert.Contains(sourceText, "BtnOpenLogsWindow_Click");
        StringAssert.Contains(sourceText, "_windowManager.ShowLogsWindow()");
    }

    [TestMethod]
    public void ContainersPageCodeBehind_OpenShellWindowClickHandler_DelegatesToAuxiliaryWindowManager()
    {
        // Arrange
        var sourceText = ReadRepositorySourceFile(@"src\WslContainersDesktop.App\Pages\ContainersPage.xaml.cs");

        // Assert: ボタンが存在するだけでなく、実際にContainerAuxiliaryWindowManager経由で
        // シェルウィンドウを開く呼び出しへ配線されていること。
        StringAssert.Contains(sourceText, "BtnOpenShellWindow_Click");
        StringAssert.Contains(sourceText, "_windowManager.ShowShellWindow()");
    }

    private static string ReadRepositorySourceFile(string relativePath)
    {
        var repositoryRoot = FindRepositoryRoot();
        var normalizedRelativePath = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(repositoryRoot, normalizedRelativePath);

        Assert.IsTrue(File.Exists(fullPath), $"Expected source file '{relativePath}' to exist at '{fullPath}'.");
        return File.ReadAllText(fullPath);
    }

    private static string FindRepositoryRoot()
    {
        var currentDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        while (currentDirectory is not null)
        {
            var appProjectFilePath = Path.Combine(currentDirectory.FullName, "src", "WslContainersDesktop.App", "WslContainersDesktop.App.csproj");
            if (File.Exists(appProjectFilePath))
            {
                return currentDirectory.FullName;
            }

            currentDirectory = currentDirectory.Parent;
        }

        Assert.Fail($"Could not locate repository root from '{AppContext.BaseDirectory}'.");
        return string.Empty;
    }
}
