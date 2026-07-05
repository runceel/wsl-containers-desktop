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
