using WslContainersDesktop_App.Navigation;
using WslContainersDesktop_App.ViewModels;

namespace WslContainersDesktop_App_Tests.ViewModels;

[TestClass]
public sealed class NavigationViewModelTests
{
    [TestMethod]
    public void Constructor_NoArguments_CurrentPageKeyIsContainers()
    {
        // Arrange & Act
        var sut = new NavigationViewModel();

        // Assert
        Assert.AreEqual(NavigationPageKey.Containers, sut.CurrentPageKey);
    }

    [TestMethod]
    [DataRow(NavigationPageKey.Containers)]
    [DataRow(NavigationPageKey.Settings)]
    [DataRow(NavigationPageKey.Volumes)]
    [DataRow(NavigationPageKey.Networks)]
    public void Constructor_InitialPageKeySpecified_CurrentPageKeyMatchesArgument(NavigationPageKey initialPageKey)
    {
        // Arrange & Act
        var sut = new NavigationViewModel(initialPageKey);

        // Assert
        Assert.AreEqual(initialPageKey, sut.CurrentPageKey);
    }

    [TestMethod]
    public void Constructor_UndefinedPageKey_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var undefinedPageKey = (NavigationPageKey)99;

        // Act & Assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new NavigationViewModel(undefinedPageKey));
    }

    [TestMethod]
    public void NavigateTo_Settings_CurrentPageKeyChangesToSettings()
    {
        // Arrange
        var sut = new NavigationViewModel();

        // Act
        sut.NavigateToCommand.Execute(NavigationPageKey.Settings);

        // Assert
        Assert.AreEqual(NavigationPageKey.Settings, sut.CurrentPageKey);
    }

    [TestMethod]
    public void NavigateTo_Containers_FromSettings_CurrentPageKeyChangesToContainers()
    {
        // Arrange
        var sut = new NavigationViewModel(NavigationPageKey.Settings);

        // Act
        sut.NavigateToCommand.Execute(NavigationPageKey.Containers);

        // Assert
        Assert.AreEqual(NavigationPageKey.Containers, sut.CurrentPageKey);
    }

    [TestMethod]
    public void NavigateTo_SameKeyAsCurrent_PropertyChangedIsNotRaisedAgain()
    {
        // Arrange
        var sut = new NavigationViewModel();
        var raisedCount = 0;
        sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NavigationViewModel.CurrentPageKey))
            {
                raisedCount++;
            }
        };

        // Act
        // 既定値のContainersと同じキーへ遷移させても変化はない想定。
        sut.NavigateToCommand.Execute(NavigationPageKey.Containers);

        // Assert
        Assert.AreEqual(0, raisedCount);
    }

    [TestMethod]
    public void NavigateTo_DifferentKey_RaisesPropertyChangedOnceForCurrentPageKey()
    {
        // Arrange
        var sut = new NavigationViewModel();
        var raisedCount = 0;
        sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(NavigationViewModel.CurrentPageKey))
            {
                raisedCount++;
            }
        };

        // Act
        sut.NavigateToCommand.Execute(NavigationPageKey.Settings);

        // Assert
        Assert.AreEqual(1, raisedCount);
    }

    [TestMethod]
    public void NavigateTo_UndefinedPageKey_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var sut = new NavigationViewModel();
        var undefinedPageKey = (NavigationPageKey)99;

        // Act & Assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => sut.NavigateToCommand.Execute(undefinedPageKey));
    }
}
