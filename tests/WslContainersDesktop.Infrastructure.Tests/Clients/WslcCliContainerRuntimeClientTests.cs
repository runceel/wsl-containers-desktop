using WslContainersDesktop.Application.Exceptions;
using WslContainersDesktop.Domain;
using WslContainersDesktop.Infrastructure.Clients;
using WslContainersDesktop.Infrastructure.Tests.Fakes;

namespace WslContainersDesktop.Infrastructure.Tests.Clients;

[TestClass]
public sealed class WslcCliContainerRuntimeClientTests
{
    [TestMethod]
    public async Task ListContainersAsync_CliReturnsContainers_MapsJsonToContainers()
    {
        // Arrange
        // 実機の `wslc list -a --format json` で確認したスキーマに基づく。
        const string json = """
            [
              {
                "CreatedAt": 1751446800,
                "Id": "sha256:aaa",
                "Image": "nginx:latest",
                "Name": "web",
                "Ports": [],
                "State": 2,
                "StateChangedAt": 1751446801
              },
              {
                "CreatedAt": 1751443200,
                "Id": "sha256:bbb",
                "Image": "alpine:latest",
                "Name": "wcd-test",
                "Ports": [],
                "State": 3,
                "StateChangedAt": 1751443260
              }
            ]
            """;
        var runner = new FakeWslcCliRunner { Result = new(0, json, string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        var containers = await sut.ListContainersAsync();

        // Assert
        Assert.HasCount(2, containers);
        Assert.AreEqual("sha256:aaa", containers[0].Id);
        Assert.AreEqual("web", containers[0].Name);
        Assert.AreEqual("nginx:latest", containers[0].Image);
        Assert.AreEqual(ContainerState.Running, containers[0].State);
        Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(1751446800), containers[0].CreatedAt);

        Assert.AreEqual("sha256:bbb", containers[1].Id);
        Assert.AreEqual(ContainerState.Stopped, containers[1].State);
    }

    [TestMethod]
    public async Task ListContainersAsync_CliReturnsEmptyArray_ReturnsEmptyList()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, "[]", string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        var containers = await sut.ListContainersAsync();

        // Assert
        Assert.IsEmpty(containers);
    }

    [TestMethod]
    public async Task ListContainersAsync_CliArguments_AreListAllFormatJson()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, "[]", string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        await sut.ListContainersAsync();

        // Assert
        CollectionAssert.AreEqual(new[] { "list", "-a", "--format", "json" }, runner.Calls[0].ToList());
    }

    [TestMethod]
    public async Task ListContainersAsync_CliExitsWithNonZeroCode_ThrowsContainerRuntimeExceptionWithStandardError()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(1, string.Empty, "一覧の取得に失敗しました。") };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act & Assert
        var ex = await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(() => sut.ListContainersAsync());
        Assert.AreEqual(1, ex.ExitCode);
        StringAssert.Contains(ex.Message, "一覧の取得に失敗しました。");
    }

    [TestMethod]
    public async Task ListContainersAsync_CliReturnsMalformedJson_ThrowsContainerRuntimeExceptionWithInnerException()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, "not-json", string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act & Assert
        var ex = await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(() => sut.ListContainersAsync());
        Assert.IsNotNull(ex.InnerException);
    }

    [TestMethod]
    public async Task StartAsync_CliSucceeds_CompletesWithoutException()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, string.Empty, string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        await sut.StartAsync("c1");

        // Assert
        CollectionAssert.AreEqual(new[] { "container", "start", "c1" }, runner.Calls[0].ToList());
    }

    [TestMethod]
    public async Task StartAsync_CliExitsWithNonZeroCode_ThrowsContainerRuntimeException()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(1, string.Empty, "起動に失敗しました。") };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act & Assert
        var ex = await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(() => sut.StartAsync("c1"));
        StringAssert.Contains(ex.Message, "起動に失敗しました。");
    }

    [TestMethod]
    public async Task StopAsync_CliSucceeds_CallsContainerStopWithId()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, string.Empty, string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        await sut.StopAsync("c1");

        // Assert
        CollectionAssert.AreEqual(new[] { "container", "stop", "c1" }, runner.Calls[0].ToList());
    }

    [TestMethod]
    public async Task StopAsync_CliExitsWithNonZeroCode_ThrowsContainerRuntimeException()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(1, string.Empty, "停止に失敗しました。") };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act & Assert
        await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(() => sut.StopAsync("c1"));
    }

    [TestMethod]
    public async Task DeleteAsync_CliSucceeds_CallsContainerRemoveWithIdWithoutForceFlag()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, string.Empty, string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        await sut.DeleteAsync("c1");

        // Assert
        // -f を付けないことで、実行中コンテナの削除をwslc自体に拒否させる
        // （ADR-0009）。
        CollectionAssert.AreEqual(new[] { "container", "remove", "c1" }, runner.Calls[0].ToList());
    }

    [TestMethod]
    public async Task DeleteAsync_CliExitsWithNonZeroCode_ThrowsContainerRuntimeExceptionWithStandardError()
    {
        // Arrange
        var runner = new FakeWslcCliRunner
        {
            Result = new(1, string.Empty, "エラー コード: WSLC_E_CONTAINER_IS_RUNNING"),
        };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act & Assert
        var ex = await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(() => sut.DeleteAsync("c1"));
        StringAssert.Contains(ex.Message, "WSLC_E_CONTAINER_IS_RUNNING");
    }
}
