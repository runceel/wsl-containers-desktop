using WslContainersDesktop.Application.Exceptions;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Infrastructure.Clients;
using WslContainersDesktop.Infrastructure.Tests.Fakes;

namespace WslContainersDesktop.Infrastructure.Tests.Clients;

[TestClass]
public sealed class WslcCliContainerLifecycleClientTests
{
    [TestMethod]
    public async Task RunContainerAsync_RequestHasAllOptions_CliArgumentsAreDetachedRunWithOptionsAndShellCommand()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, string.Empty, string.Empty) };
        var sut = new WslcCliContainerLifecycleClient(runner);
        var request = new ContainerRunRequest
        {
            ImageReference = "nginx:latest",
            ContainerName = "web",
            RemoveWhenStopped = true,
            PortMappings = ["8080:80", "8443:443"],
            EnvironmentVariables = ["FOO=bar", "BAZ=qux"],
            Command = "echo \"hello world\"",
        };

        // Act
        await sut.RunContainerAsync(request);

        // Assert
        CollectionAssert.AreEqual(
            new[] { "run", "-d", "--rm", "--name", "web", "-p", "8080:80", "-p", "8443:443", "-e", "FOO=bar", "-e", "BAZ=qux", "nginx:latest", "/bin/sh", "-lc", "echo \"hello world\"" },
            runner.Calls[0].ToList());
    }

    [TestMethod]
    public async Task RunContainerAsync_CommandIsEmpty_CliArgumentsDoNotOverrideImageDefaultCommand()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, string.Empty, string.Empty) };
        var sut = new WslcCliContainerLifecycleClient(runner);
        var request = new ContainerRunRequest { ImageReference = "hello-world", Command = string.Empty };

        // Act
        await sut.RunContainerAsync(request);

        // Assert
        CollectionAssert.AreEqual(new[] { "run", "-d", "hello-world" }, runner.Calls[0].ToList());
    }

    [TestMethod]
    public async Task RunContainerAsync_RemoveWhenStoppedIsFalse_DoesNotEmitRmFlag()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, string.Empty, string.Empty) };
        var sut = new WslcCliContainerLifecycleClient(runner);
        var request = new ContainerRunRequest { ImageReference = "hello-world", RemoveWhenStopped = false };

        // Act
        await sut.RunContainerAsync(request);

        // Assert
        CollectionAssert.DoesNotContain(runner.Calls[0].ToList(), "--rm");
    }

    [TestMethod]
    public async Task StartAsync_CliSucceeds_CompletesWithoutException()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, string.Empty, string.Empty) };
        var sut = new WslcCliContainerLifecycleClient(runner);

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
        var sut = new WslcCliContainerLifecycleClient(runner);

        // Act & Assert
        var ex = await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(() => sut.StartAsync("c1"));
        StringAssert.Contains(ex.Message, "起動に失敗しました。");
    }

    [TestMethod]
    public async Task StopAsync_CliSucceeds_CallsContainerStopWithId()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, string.Empty, string.Empty) };
        var sut = new WslcCliContainerLifecycleClient(runner);

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
        var sut = new WslcCliContainerLifecycleClient(runner);

        // Act & Assert
        await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(() => sut.StopAsync("c1"));
    }

    [TestMethod]
    public async Task DeleteAsync_CliSucceeds_CallsContainerRemoveWithIdWithoutForceFlag()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, string.Empty, string.Empty) };
        var sut = new WslcCliContainerLifecycleClient(runner);

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
        var sut = new WslcCliContainerLifecycleClient(runner);

        // Act & Assert
        var ex = await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(() => sut.DeleteAsync("c1"));
        StringAssert.Contains(ex.Message, "WSLC_E_CONTAINER_IS_RUNNING");
    }
}
