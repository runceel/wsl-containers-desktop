using System.Globalization;
using System.Text.Json;
using WslContainersDesktop.Application.Exceptions;
using WslContainersDesktop.Infrastructure.Clients;
using WslContainersDesktop.Infrastructure.Tests.Fakes;

namespace WslContainersDesktop.Infrastructure.Tests.Clients;

[TestClass]
public sealed class WslcCliContainerStatsClientTests
{
    [TestMethod]
    public async Task GetContainerStatsAsync_Invoked_RunsStatsJsonCommand()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, "[]", string.Empty) };
        var sut = new WslcCliContainerStatsClient(runner);

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
        var sut = new WslcCliContainerStatsClient(runner);

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
        var sut = new WslcCliContainerStatsClient(runner);

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
        var sut = new WslcCliContainerStatsClient(runner);

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
        var sut = new WslcCliContainerStatsClient(runner);
        var usages = await sut.GetContainerStatsAsync();
        Assert.IsEmpty(usages);
    }

    [TestMethod]
    public async Task GetContainerStatsAsync_WhitespaceStdout_ReturnsEmptyList()
    {
        var runner = new FakeWslcCliRunner { Result = new(0, " \n\t ", string.Empty) };
        var sut = new WslcCliContainerStatsClient(runner);
        var usages = await sut.GetContainerStatsAsync();
        Assert.IsEmpty(usages);
    }

    [TestMethod]
    public async Task GetContainerStatsAsync_EmptyJsonArray_ReturnsEmptyList()
    {
        var runner = new FakeWslcCliRunner { Result = new(0, "[]", string.Empty) };
        var sut = new WslcCliContainerStatsClient(runner);
        var usages = await sut.GetContainerStatsAsync();
        Assert.IsEmpty(usages);
    }

    [TestMethod]
    public async Task GetContainerStatsAsync_InvalidJson_ThrowsContainerRuntimeException()
    {
        var runner = new FakeWslcCliRunner { Result = new(0, "not-json", string.Empty) };
        var sut = new WslcCliContainerStatsClient(runner);
        await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(() => sut.GetContainerStatsAsync());
    }

    [TestMethod]
    public async Task GetContainerStatsAsync_NonZeroExitCode_ThrowsContainerRuntimeException()
    {
        var runner = new FakeWslcCliRunner { Result = new(1, string.Empty, "boom") };
        var sut = new WslcCliContainerStatsClient(runner);
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
        var sut = new WslcCliContainerStatsClient(runner);
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
        var sut = new WslcCliContainerStatsClient(runner);
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
            var sut = new WslcCliContainerStatsClient(runner);

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
}
