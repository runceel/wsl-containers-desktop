using WslContainersDesktop.Application.Exceptions;

namespace WslContainersDesktop.Application.Tests.Exceptions;

[TestClass]
public sealed class ContainerManagementExceptionsTests
{
    [TestMethod]
    public void ContainerRuntimeException_ConstructedWithDetails_PropertiesAreSet()
    {
        // Arrange & Act
        var sut = new ContainerRuntimeException(
            command: "container start c1",
            exitCode: 1,
            message: "コンテナーを開始できませんでした。");

        // Assert
        Assert.AreEqual("container start c1", sut.Command);
        Assert.AreEqual(1, sut.ExitCode);
        Assert.AreEqual("コンテナーを開始できませんでした。", sut.Message);
        Assert.IsInstanceOfType<ContainerManagementException>(sut);
    }

    [TestMethod]
    public void ContainerRuntimeException_ConstructedWithInnerException_InnerExceptionIsSet()
    {
        // Arrange
        var inner = new InvalidOperationException("JSON解析エラー");

        // Act
        var sut = new ContainerRuntimeException(
            command: "list -a --format json",
            exitCode: 0,
            message: "コンテナ一覧の解析に失敗しました。",
            innerException: inner);

        // Assert
        Assert.AreSame(inner, sut.InnerException);
    }

    [TestMethod]
    public void InvalidContainerOperationException_ConstructedWithDetails_PropertiesAreSet()
    {
        // Arrange & Act
        var sut = new InvalidContainerOperationException(containerId: "c1", operationName: "Start");

        // Assert
        Assert.AreEqual("c1", sut.ContainerId);
        Assert.AreEqual("Start", sut.OperationName);
        StringAssert.Contains(sut.Message, "c1");
        StringAssert.Contains(sut.Message, "Start");
        Assert.IsInstanceOfType<ContainerManagementException>(sut);
    }

    [TestMethod]
    public void ContainerNotFoundException_ConstructedWithContainerId_PropertyIsSetAndMessageContainsId()
    {
        // Arrange & Act
        var sut = new ContainerNotFoundException(containerId: "c1");

        // Assert
        Assert.AreEqual("c1", sut.ContainerId);
        StringAssert.Contains(sut.Message, "c1");
        Assert.IsInstanceOfType<ContainerManagementException>(sut);
    }
}
