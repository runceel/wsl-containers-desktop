using WslContainersDesktop.Infrastructure.Cli;
using WslContainersDesktop.Infrastructure.Tests.Fakes;
using WslContainersDesktop.Infrastructure.Wsl;

namespace WslContainersDesktop.Infrastructure.Tests.Wsl;

[TestClass]
public sealed class WslEnvironmentProbeTests
{
    private const string WindowsVersionSample =
        "WSL version: 2.9.3.0\r\n" +
        "Kernel version: 5.15.167.4-1\r\n" +
        "WSLg version: 1.0.65\r\n" +
        "Windows version: 10.0.26100.1\r\n";

    private static WslEnvironmentProbe CreateSut(FakeWslCommandRunner runner, FakeWslcExecutableProbe wslcProbe)
        => new(runner, wslcProbe);

    [TestMethod]
    public async Task GetEnvironmentInfoAsync_VersionOutputAndWslcAvailable_ReturnsParsedVersionAndAvailableTrue()
    {
        // Arrange
        var runner = new FakeWslCommandRunner { Result = new CliResult(0, WindowsVersionSample, string.Empty) };
        var wslcProbe = new FakeWslcExecutableProbe { Available = true };
        var sut = CreateSut(runner, wslcProbe);

        // Act
        var info = await sut.GetEnvironmentInfoAsync();

        // Assert
        Assert.AreEqual("2.9.3.0", info.WslVersion);
        Assert.IsTrue(info.IsWslContainersAvailable);
    }

    [TestMethod]
    public async Task GetEnvironmentInfoAsync_LocalizedVersionLabel_ParsesFirstVersionToken()
    {
        // Arrange
        var localized =
            "WSL バージョン: 2.9.3.0\r\n" +
            "カーネル バージョン: 5.15.167.4-1\r\n";
        var runner = new FakeWslCommandRunner { Result = new CliResult(0, localized, string.Empty) };
        var wslcProbe = new FakeWslcExecutableProbe { Available = true };
        var sut = CreateSut(runner, wslcProbe);

        // Act
        var info = await sut.GetEnvironmentInfoAsync();

        // Assert
        Assert.AreEqual("2.9.3.0", info.WslVersion);
    }

    [TestMethod]
    public async Task GetEnvironmentInfoAsync_WslcNotAvailable_AvailableIsFalse()
    {
        // Arrange
        var runner = new FakeWslCommandRunner { Result = new CliResult(0, WindowsVersionSample, string.Empty) };
        var wslcProbe = new FakeWslcExecutableProbe { Available = false };
        var sut = CreateSut(runner, wslcProbe);

        // Act
        var info = await sut.GetEnvironmentInfoAsync();

        // Assert
        Assert.AreEqual("2.9.3.0", info.WslVersion);
        Assert.IsFalse(info.IsWslContainersAvailable);
    }

    [TestMethod]
    public async Task GetEnvironmentInfoAsync_WslCommandExitsNonZero_VersionIsNull()
    {
        // Arrange
        var runner = new FakeWslCommandRunner { Result = new CliResult(1, string.Empty, "'wsl' is not recognized") };
        var wslcProbe = new FakeWslcExecutableProbe { Available = false };
        var sut = CreateSut(runner, wslcProbe);

        // Act
        var info = await sut.GetEnvironmentInfoAsync();

        // Assert
        Assert.IsNull(info.WslVersion);
        Assert.IsFalse(info.IsWslDetected);
    }

    [TestMethod]
    public async Task GetEnvironmentInfoAsync_WslCommandThrows_VersionIsNull()
    {
        // Arrange
        var runner = new FakeWslCommandRunner { RunException = new InvalidOperationException("failed to start wsl.exe") };
        var wslcProbe = new FakeWslcExecutableProbe { Available = true };
        var sut = CreateSut(runner, wslcProbe);

        // Act
        var info = await sut.GetEnvironmentInfoAsync();

        // Assert
        Assert.IsNull(info.WslVersion);
        Assert.IsTrue(info.IsWslContainersAvailable);
    }

    [TestMethod]
    public async Task GetEnvironmentInfoAsync_NoVersionInOutput_VersionIsNull()
    {
        // Arrange
        var runner = new FakeWslCommandRunner { Result = new CliResult(0, "no version information here", string.Empty) };
        var wslcProbe = new FakeWslcExecutableProbe { Available = true };
        var sut = CreateSut(runner, wslcProbe);

        // Act
        var info = await sut.GetEnvironmentInfoAsync();

        // Assert
        Assert.IsNull(info.WslVersion);
    }

    [TestMethod]
    public async Task GetEnvironmentInfoAsync_Always_InvokesWslWithVersionArgument()
    {
        // Arrange
        var runner = new FakeWslCommandRunner { Result = new CliResult(0, WindowsVersionSample, string.Empty) };
        var wslcProbe = new FakeWslcExecutableProbe { Available = true };
        var sut = CreateSut(runner, wslcProbe);

        // Act
        await sut.GetEnvironmentInfoAsync();

        // Assert
        Assert.HasCount(1, runner.Calls);
        CollectionAssert.AreEqual(new[] { "--version" }, runner.Calls[0].ToList());
    }
}
