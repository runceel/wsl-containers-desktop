using WslContainersDesktop.Infrastructure.Cli;

namespace WslContainersDesktop.Infrastructure.Tests.Cli;

[TestClass]
public sealed class WslcCliRunnerTests
{
    [TestMethod]
    public async Task RunAsync_CommandWritesToStandardOutput_ReturnsExitCodeZeroAndStandardOutput()
    {
        // Arrange
        // 実在のwslc.exeに依存せず、Windows標準のcmd.exeで代替してプロセス起動の
        // 仕組みそのものを検証する。
        var sut = new WslcCliRunner(executablePath: "cmd.exe");

        // Act
        var result = await sut.RunAsync(["/c", "echo hello-stdout"]);

        // Assert
        Assert.AreEqual(0, result.ExitCode);
        StringAssert.Contains(result.StandardOutput, "hello-stdout");
    }

    [TestMethod]
    public async Task RunAsync_CommandWritesToStandardError_StandardErrorIsCapturedSeparatelyFromStandardOutput()
    {
        // Arrange
        var sut = new WslcCliRunner(executablePath: "cmd.exe");

        // Act
        var result = await sut.RunAsync(["/c", "echo hello-stderr 1>&2"]);

        // Assert
        StringAssert.Contains(result.StandardError, "hello-stderr");
        StringAssert.DoesNotMatch(result.StandardOutput, new System.Text.RegularExpressions.Regex("hello-stderr"));
    }

    [TestMethod]
    public async Task RunAsync_CommandExitsWithNonZeroCode_ExitCodeIsReturned()
    {
        // Arrange
        var sut = new WslcCliRunner(executablePath: "cmd.exe");

        // Act
        var result = await sut.RunAsync(["/c", "exit 3"]);

        // Assert
        Assert.AreEqual(3, result.ExitCode);
    }

    [TestMethod]
    public void Constructor_NoExecutablePathSpecified_DefaultsToWslc()
    {
        // Arrange & Act
        var sut = new WslcCliRunner();

        // Assert
        Assert.AreEqual("wslc", sut.ExecutablePath);
    }
}
