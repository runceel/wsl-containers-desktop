using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using WslContainersDesktop.Application.Exceptions;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;
using WslContainersDesktop.Infrastructure.Cli;
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
    public async Task ListImagesAsync_CliReturnsImages_MapsJsonToContainerImages()
    {
        // Arrange
        const string json = """
            [{
              "Created": 1782533899,
              "Id": "sha256:fb3bcc37a9d41b510f9bdb8ec8e66884578aa44b9703f77cef905db46c6557e5",
              "Repository": "ubuntu",
              "Size": 120033654,
              "Tag": "latest"
            }]
            """;
        var runner = new FakeWslcCliRunner { Result = new(0, json, string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        var images = await sut.ListImagesAsync();

        // Assert
        Assert.HasCount(1, images);
        Assert.AreEqual("sha256:fb3bcc37a9d41b510f9bdb8ec8e66884578aa44b9703f77cef905db46c6557e5", images[0].Id);
        Assert.AreEqual("ubuntu", images[0].Repository);
        Assert.AreEqual("latest", images[0].Tag);
        Assert.AreEqual(120033654L, images[0].SizeBytes);
        Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(1782533899), images[0].CreatedAt);
    }

    [TestMethod]
    public async Task ListImagesAsync_CliArguments_AreImageListJsonNoTrunc()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, "[]", string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        await sut.ListImagesAsync();

        // Assert
        CollectionAssert.AreEqual(new[] { "image", "list", "--format", "json", "--no-trunc" }, runner.Calls[0].ToList());
    }

    [TestMethod]
    public async Task ListImagesAsync_CliReturnsMalformedJson_ThrowsContainerRuntimeExceptionWithInnerException()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, "not-json", string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act & Assert
        var ex = await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(() => sut.ListImagesAsync());
        Assert.IsNotNull(ex.InnerException);
    }

    [TestMethod]
    public async Task PullImageAsync_CliArguments_AreTopLevelPullWithImageReference()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, string.Empty, string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        await sut.PullImageAsync("ubuntu:latest");

        // Assert
        CollectionAssert.AreEqual(new[] { "pull", "ubuntu:latest" }, runner.Calls[0].ToList());
    }

    [TestMethod]
    public async Task DeleteImageAsync_CliArguments_AreImageRemoveWithoutForce()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, string.Empty, string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        await sut.DeleteImageAsync("sha256:abc");

        // Assert
        var arguments = runner.Calls[0].ToList();
        CollectionAssert.AreEqual(new[] { "image", "remove", "sha256:abc" }, arguments);
        CollectionAssert.DoesNotContain(arguments, "--force");
    }

    [TestMethod]
    public async Task GetContainerLogsAsync_CliReturnsLinesWithTrailingNewline_ReturnsLinesWithoutTrailingPhantomEmpty()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, "one\ntwo\n", string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        var logs = await sut.GetContainerLogsAsync("c1");

        // Assert
        CollectionAssert.AreEqual(new[] { "one", "two" }, logs.ToList());
    }

    [TestMethod]
    public async Task GetContainerLogsAsync_CliOutputWithoutTrailingNewline_KeepsLastLine()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, "one\ntwo", string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        var logs = await sut.GetContainerLogsAsync("c1");

        // Assert
        CollectionAssert.AreEqual(new[] { "one", "two" }, logs.ToList());
    }

    [TestMethod]
    public async Task GetContainerLogsAsync_CliReturnsCrLf_TrimsCarriageReturns()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, "one\r\ntwo\r\n", string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        var logs = await sut.GetContainerLogsAsync("c1");

        // Assert
        CollectionAssert.AreEqual(new[] { "one", "two" }, logs.ToList());
    }

    [TestMethod]
    public async Task GetContainerLogsAsync_CliReturnsEmptyOutput_ReturnsEmptyList()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, string.Empty, string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        var logs = await sut.GetContainerLogsAsync("c1");

        // Assert
        Assert.IsEmpty(logs);
    }

    [TestMethod]
    public async Task GetContainerLogsAsync_CliArguments_AreContainerLogsWithId()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, string.Empty, string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        await sut.GetContainerLogsAsync("c1");

        // Assert
        CollectionAssert.AreEqual(new[] { "container", "logs", "c1" }, runner.Calls[0].ToList());
    }

    [TestMethod]
    public async Task GetContainerLogsAsync_CliReturnsStdErrWithExitZero_IncludesStdErrAsLogContent()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, "one\n", "two\n") };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        var logs = await sut.GetContainerLogsAsync("c1");

        // Assert
        CollectionAssert.AreEqual(new[] { "one", "two" }, logs.ToList());
    }

    [TestMethod]
    public async Task GetContainerLogsAsync_CliExitsWithNonZero_ThrowsContainerRuntimeException()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(1, string.Empty, "失敗しました。") };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act & Assert
        var ex = await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(() => sut.GetContainerLogsAsync("c1"));
        Assert.AreEqual(1, ex.ExitCode);
    }

    [TestMethod]
    public async Task FollowContainerLogsAsync_CliArguments_AreContainerLogsSinceFollowWithId()
    {
        // Arrange
        var runner = new FakeWslcCliRunner();
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        await foreach (var _ in sut.FollowContainerLogsAsync("c1"))
        {
        }

        // Assert
        var arguments = runner.StreamCalls[0].ToList();
        CollectionAssert.AreEqual(new[] { "container", "logs", "--since" }, arguments.Take(3).ToList());
        Assert.IsTrue(long.TryParse(arguments[3], out _));
        CollectionAssert.AreEqual(new[] { "--follow", "c1" }, arguments.Skip(4).ToList());
    }

    [TestMethod]
    public async Task FollowContainerLogsAsync_CliStreamsLines_ReturnsLinesInArrivalOrder()
    {
        // Arrange
        var runner = new FakeWslcCliRunner();
        runner.StreamLinesAsyncFunc = (arguments, cancellationToken) => CreateLinesAsync("one", "two");
        var sut = new WslcCliContainerRuntimeClient(runner);
        var actual = new List<string>();

        // Act
        await foreach (var line in sut.FollowContainerLogsAsync("c1"))
        {
            actual.Add(line);
        }

        // Assert
        CollectionAssert.AreEqual(new[] { "one", "two" }, actual);
    }

    [TestMethod]
    public async Task FollowContainerLogsAsync_CliStreamExitsNonZero_ThrowsContainerRuntimeException()
    {
        // Arrange
        var runner = new FakeWslcCliRunner();
        runner.StreamLinesException = new CliStreamException("container logs --since 1783057407 --follow c1", 1, "追跡に失敗しました。");
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act & Assert
        await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(async () =>
        {
            await foreach (var _ in sut.FollowContainerLogsAsync("c1"))
            {
            }
        });
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
        var sut = new WslcCliContainerRuntimeClient(runner);

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
        var sut = new WslcCliContainerRuntimeClient(runner);

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
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act & Assert
        var ex = await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(() => sut.GetContainerDetailAsync("c1"));
        Assert.IsNotNull(ex.InnerException);
    }

    [TestMethod]
    public async Task OpenExecSessionAsync_CliArguments_AreContainerExecInteractiveNoTtyShell()
    {
        // Arrange
        var expected = new FakeContainerExecSession();
        var runner = new FakeWslcCliRunner { ExecSession = expected };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        var session = await sut.OpenExecSessionAsync("c1");

        // Assert
        Assert.AreSame(expected, session);
        CollectionAssert.AreEqual(new[] { "container", "exec", "-i", "c1", "/bin/sh" }, runner.OpenInteractiveCalls[0].ToList());
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

    [TestMethod]
    public async Task GetContainerStatsAsync_Invoked_RunsStatsJsonCommand()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, "[]", string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        await sut.GetContainerStatsAsync();

        // Assert
        // 実機 wslc 2.9.3.0 の `stats` はスナップショット出力のため `--no-stream` は
        // 存在しない（指定するとexit code 1で失敗する）。正しいコマンドは
        // `stats --format json`（Phase 5 実機確認）。
        Assert.HasCount(1, runner.Calls);
        CollectionAssert.AreEqual(new[] { "stats", "--format", "json" }, runner.Calls[0].ToList());
    }

    [TestMethod]
    public async Task GetContainerStatsAsync_RealWslcSpacedFormat_MapsToResourceUsages()
    {
        // Arrange
        // 実機 wslc 2.9.3.0 `stats --format json` の実出力（単位前に空白あり）。
        var json = JsonSerializer.Serialize(new[]
        {
            new
            {
                BlockIO = "0 B / 0 B",
                CPUPerc = "0.00%",
                ID = "467c10b465c7e66ddda93121224d8506420a5200ab9f7e50c5edbc79fda74aae",
                MemPerc = "0.01%",
                MemUsage = "1.82 MiB / 15.37 GiB",
                Name = "wcd-demo-idle-142257",
                NetIO = "736 B / 0 B",
                PIDs = 2,
            },
        });
        var runner = new FakeWslcCliRunner { Result = new(0, json, string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        var usages = await sut.GetContainerStatsAsync();

        // Assert
        Assert.HasCount(1, usages);
        Assert.AreEqual("467c10b465c7e66ddda93121224d8506420a5200ab9f7e50c5edbc79fda74aae", usages[0].ContainerId);
        Assert.AreEqual("wcd-demo-idle-142257", usages[0].Name);
        Assert.AreEqual(0.0, usages[0].CpuPercentage, 1e-9);
        Assert.AreEqual((long)Math.Round(1.82 * 1024 * 1024, MidpointRounding.AwayFromZero), usages[0].MemoryUsageBytes);
        Assert.AreEqual((long)Math.Round(15.37 * 1024d * 1024 * 1024, MidpointRounding.AwayFromZero), usages[0].MemoryLimitBytes);
    }

    [TestMethod]
    public async Task GetContainerStatsAsync_JsonArray_MapsToResourceUsages()
    {
        // Arrange
        var json = JsonSerializer.Serialize(new[]
        {
            new { ID = "sha256:aaa", Name = "web", CPUPerc = "12.34%", MemUsage = "512MiB / 2GiB" },
            new { ID = "sha256:bbb", Name = "db", CPUPerc = "0.00%", MemUsage = "128MiB / 1GiB" },
        });
        var runner = new FakeWslcCliRunner { Result = new(0, json, string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        var usages = await sut.GetContainerStatsAsync();

        // Assert
        Assert.HasCount(2, usages);
        Assert.AreEqual("sha256:aaa", usages[0].ContainerId);
        Assert.AreEqual("web", usages[0].Name);
        Assert.AreEqual(12.34, usages[0].CpuPercentage, 1e-9);
        Assert.AreEqual(536870912L, usages[0].MemoryUsageBytes);
        Assert.AreEqual(2147483648L, usages[0].MemoryLimitBytes);
        Assert.AreEqual("sha256:bbb", usages[1].ContainerId);
        Assert.AreEqual("db", usages[1].Name);
        Assert.AreEqual(0.0, usages[1].CpuPercentage, 1e-9);
        Assert.AreEqual(134217728L, usages[1].MemoryUsageBytes);
        Assert.AreEqual(1073741824L, usages[1].MemoryLimitBytes);
    }

    [TestMethod]
    public async Task GetContainerStatsAsync_Ndjson_MapsToResourceUsages()
    {
        // Arrange
        var ndjson = string.Join(
            "\n",
            JsonSerializer.Serialize(new { ID = "sha256:aaa", Name = "web", CPUPerc = "12.34%", MemUsage = "512MiB / 2GiB" }),
            JsonSerializer.Serialize(new { ID = "sha256:bbb", Name = "db", CPUPerc = "0.00%", MemUsage = "128MiB / 1GiB" }));
        var runner = new FakeWslcCliRunner { Result = new(0, ndjson, string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        var usages = await sut.GetContainerStatsAsync();

        // Assert
        Assert.HasCount(2, usages);
        Assert.AreEqual("sha256:aaa", usages[0].ContainerId);
        Assert.AreEqual(536870912L, usages[0].MemoryUsageBytes);
        Assert.AreEqual(2147483648L, usages[0].MemoryLimitBytes);
        Assert.AreEqual("sha256:bbb", usages[1].ContainerId);
        Assert.AreEqual(134217728L, usages[1].MemoryUsageBytes);
        Assert.AreEqual(1073741824L, usages[1].MemoryLimitBytes);
    }

    [TestMethod]
    public async Task GetContainerStatsAsync_EmptyStdout_ReturnsEmptyList()
    {
        var runner = new FakeWslcCliRunner { Result = new(0, string.Empty, string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);
        var usages = await sut.GetContainerStatsAsync();
        Assert.IsEmpty(usages);
    }

    [TestMethod]
    public async Task GetContainerStatsAsync_WhitespaceStdout_ReturnsEmptyList()
    {
        var runner = new FakeWslcCliRunner { Result = new(0, " \n\t ", string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);
        var usages = await sut.GetContainerStatsAsync();
        Assert.IsEmpty(usages);
    }

    [TestMethod]
    public async Task GetContainerStatsAsync_EmptyJsonArray_ReturnsEmptyList()
    {
        var runner = new FakeWslcCliRunner { Result = new(0, "[]", string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);
        var usages = await sut.GetContainerStatsAsync();
        Assert.IsEmpty(usages);
    }

    [TestMethod]
    public async Task GetContainerStatsAsync_InvalidJson_ThrowsContainerRuntimeException()
    {
        var runner = new FakeWslcCliRunner { Result = new(0, "not-json", string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);
        await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(() => sut.GetContainerStatsAsync());
    }

    [TestMethod]
    public async Task GetContainerStatsAsync_NonZeroExitCode_ThrowsContainerRuntimeException()
    {
        var runner = new FakeWslcCliRunner { Result = new(1, string.Empty, "boom") };
        var sut = new WslcCliContainerRuntimeClient(runner);
        await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(() => sut.GetContainerStatsAsync());
    }

    [DataTestMethod]
    [DataRow("12.34%", 12.34)]
    [DataRow("0.00%", 0.0)]
    [DataRow("5%", 5.0)]
    [DataRow("100.00%", 100.0)]
    [DataRow("--", 0.0)]
    [DataRow("", 0.0)]
    public async Task GetContainerStatsAsync_CpuPercVariants_ParsesCpuPercentage(string cpuString, double expected)
    {
        var runner = new FakeWslcCliRunner { Result = new(0, SingleStatJson("c1", "n", cpuString, "100MiB / 200MiB"), string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);
        var usages = await sut.GetContainerStatsAsync();
        Assert.AreEqual(expected, usages[0].CpuPercentage, 1e-9);
    }

    [DataTestMethod]
    [DataRow("512MiB / 2GiB", 536870912L, 2147483648L)]
    [DataRow("1.5GiB / 4GiB", 1610612736L, 4294967296L)]
    [DataRow("128MB / 1GB", 128000000L, 1000000000L)]
    [DataRow("500kB / 2MB", 500000L, 2000000L)]
    [DataRow("1024B / 2048B", 1024L, 2048L)]
    [DataRow("0B / 0B", 0L, 0L)]
    [DataRow("512MiB", 536870912L, 0L)]
    public async Task GetContainerStatsAsync_MemUsageVariants_ParsesMemoryBytes(string memString, long expectedUsage, long expectedLimit)
    {
        var runner = new FakeWslcCliRunner { Result = new(0, SingleStatJson("c1", "n", "0.00%", memString), string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);
        var usages = await sut.GetContainerStatsAsync();
        Assert.AreEqual(expectedUsage, usages[0].MemoryUsageBytes);
        Assert.AreEqual(expectedLimit, usages[0].MemoryLimitBytes);
    }

    [TestMethod]
    public async Task GetContainerStatsAsync_UnderNonInvariantCulture_ParsesDecimalPoint()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = new CultureInfo("fr-FR");

            var runner = new FakeWslcCliRunner { Result = new(0, SingleStatJson("c1", "n", "12.34%", "1.5GiB / 4GiB"), string.Empty) };
            var sut = new WslcCliContainerRuntimeClient(runner);

            var usages = await sut.GetContainerStatsAsync();

            Assert.AreEqual(12.34, usages[0].CpuPercentage, 1e-9);
            Assert.AreEqual(1610612736L, usages[0].MemoryUsageBytes);
            Assert.AreEqual(4294967296L, usages[0].MemoryLimitBytes);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    private static string SingleStatJson(string id, string name, string cpu, string mem)
        => JsonSerializer.Serialize(new[] { new { ID = id, Name = name, CPUPerc = cpu, MemUsage = mem } });

    private static async IAsyncEnumerable<string> CreateLinesAsync(params string[] lines)
    {
        foreach (var line in lines)
        {
            yield return line;
        }
    }

    private sealed class FakeContainerExecSession : IContainerExecSession
    {
        public bool IsClosed => false;

        public async IAsyncEnumerable<string> ReadOutputAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public Task SendCommandAsync(string command, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
