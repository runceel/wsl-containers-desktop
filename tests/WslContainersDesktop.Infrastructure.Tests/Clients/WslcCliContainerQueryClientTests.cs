using WslContainersDesktop.Application.Exceptions;
using WslContainersDesktop.Domain;
using WslContainersDesktop.Infrastructure.Clients;
using WslContainersDesktop.Infrastructure.Tests.Fakes;

namespace WslContainersDesktop.Infrastructure.Tests.Clients;

[TestClass]
public sealed class WslcCliContainerQueryClientTests
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
        var sut = new WslcCliContainerQueryClient(runner);

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
        var sut = new WslcCliContainerQueryClient(runner);

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
        var sut = new WslcCliContainerQueryClient(runner);

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
        var sut = new WslcCliContainerQueryClient(runner);

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
        var sut = new WslcCliContainerQueryClient(runner);

        // Act & Assert
        var ex = await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(() => sut.ListContainersAsync());
        Assert.IsNotNull(ex.InnerException);
    }

    [TestMethod]
    public async Task GetContainerDetailAsync_CliReturnsInspectArray_MapsJsonToDetail()
    {
        // Arrange
        const string json = """
            [{
              "Id": "sha256:abc",
              "Name": "web",
              "Created": "2026-07-04T00:00:00Z",
              "Image": "sha256:image",
              "State": {
                "Status": "exited",
                "Running": false,
                "ExitCode": 137,
                "StartedAt": "2026-07-04T00:01:00Z",
                "FinishedAt": "2026-07-04T00:02:00Z"
              },
              "Config": {
                "Env": ["A=1", "B=two=three"],
                "Cmd": ["sleep", "infinity"],
                "Entrypoint": ["/entrypoint.sh"]
              },
              "Ports": {
                "80/tcp": [{ "HostIp": "127.0.0.1", "HostPort": "8080" }],
                "443/tcp": []
              },
              "Mounts": [
                { "Type": "bind", "Source": "C:\\data", "Destination": "/data", "ReadWrite": false }
              ],
              "NetworkSettings": {
                "Networks": {
                  "bridge": { "IPAddress": "172.17.0.2" }
                }
              }
            }]
            """;
        var runner = new FakeWslcCliRunner { Result = new(0, json, string.Empty) };
        var sut = new WslcCliContainerQueryClient(runner);

        // Act
        var detail = await sut.GetContainerDetailAsync("c1");

        // Assert
        Assert.AreEqual("sha256:abc", detail.Id);
        Assert.AreEqual("web", detail.Name);
        Assert.AreEqual(ContainerState.Stopped, detail.State);
        Assert.AreEqual(new DateTimeOffset(2026, 7, 4, 0, 0, 0, TimeSpan.Zero), detail.CreatedAt);
        Assert.AreEqual("sleep infinity", detail.Command);
        Assert.AreEqual("/entrypoint.sh", detail.Entrypoint);
        Assert.AreEqual(137, detail.RunState.ExitCode);
        Assert.AreEqual(new DateTimeOffset(2026, 7, 4, 0, 1, 0, TimeSpan.Zero), detail.RunState.StartedAt);
        Assert.AreEqual("A", detail.Environment[0].Name);
        Assert.AreEqual("1", detail.Environment[0].Value);
        Assert.AreEqual("B", detail.Environment[1].Name);
        Assert.AreEqual("two=three", detail.Environment[1].Value);
        Assert.AreEqual("127.0.0.1", detail.Ports[0].HostAddress);
        Assert.AreEqual((ushort)8080, detail.Ports[0].HostPort);
        Assert.AreEqual((ushort)80, detail.Ports[0].ContainerPort);
        Assert.AreEqual("tcp", detail.Ports[0].Protocol);
        Assert.IsNull(detail.Ports[1].HostAddress);
        Assert.IsNull(detail.Ports[1].HostPort);
        Assert.AreEqual((ushort)443, detail.Ports[1].ContainerPort);
        Assert.AreEqual("/data", detail.Mounts[0].Target);
        Assert.IsTrue(detail.Mounts[0].IsReadOnly);
        Assert.AreEqual("bridge", detail.Networks[0].Name);
        Assert.AreEqual("172.17.0.2", detail.Networks[0].IpAddress);
    }

    [TestMethod]
    public async Task GetContainerDetailAsync_CliArguments_AreContainerInspectWithId()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, "[]", string.Empty) };
        var sut = new WslcCliContainerQueryClient(runner);

        // Act
        await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(() => sut.GetContainerDetailAsync("c1"));

        // Assert
        CollectionAssert.AreEqual(new[] { "container", "inspect", "c1" }, runner.Calls[0].ToList());
    }

    [TestMethod]
    public async Task GetContainerDetailAsync_CliReturnsMalformedJson_ThrowsContainerRuntimeExceptionWithInnerException()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, "not-json", string.Empty) };
        var sut = new WslcCliContainerQueryClient(runner);

        // Act & Assert
        var ex = await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(() => sut.GetContainerDetailAsync("c1"));
        Assert.IsNotNull(ex.InnerException);
    }
}
