using WslContainersDesktop_App.Navigation;
using WslContainersDesktop_App.Pages;

namespace WslContainersDesktop_App_Tests.Navigation;

[TestClass]
public sealed class NavigationPageRegistryTests
{
    [TestMethod]
    [DataRow(NavigationPageKey.Dashboard, typeof(DashboardPage))]
    [DataRow(NavigationPageKey.Containers, typeof(ContainersPage))]
    [DataRow(NavigationPageKey.Images, typeof(ImagesPage))]
    [DataRow(NavigationPageKey.Volumes, typeof(VolumesPage))]
    [DataRow(NavigationPageKey.Networks, typeof(NetworksPage))]
    [DataRow(NavigationPageKey.Settings, typeof(SettingsPage))]
    public void GetPageType_DefinedPageKey_ReturnsExpectedPageType(NavigationPageKey pageKey, Type expectedPageType)
    {
        // Act
        var pageType = NavigationPageRegistry.GetPageType(pageKey);

        // Assert
        Assert.AreEqual(expectedPageType, pageType);
    }

    [TestMethod]
    public void GetPageType_AllDefinedPageKeys_MapToDistinctPageTypes()
    {
        // Arrange
        var allPageKeys = Enum.GetValues<NavigationPageKey>();

        // Act
        var pageTypes = allPageKeys.Select(NavigationPageRegistry.GetPageType).ToList();

        // Assert
        // NavigationPageKeyに新しい値が追加された際の登録漏れ、および複数キーが同じページ型を
        // 指す誤登録の両方を早期検出する。
        CollectionAssert.AllItemsAreUnique(pageTypes);
    }

    [TestMethod]
    public void GetPageType_UndefinedPageKey_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var undefinedPageKey = (NavigationPageKey)99;

        // Act & Assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => NavigationPageRegistry.GetPageType(undefinedPageKey));
    }
}
