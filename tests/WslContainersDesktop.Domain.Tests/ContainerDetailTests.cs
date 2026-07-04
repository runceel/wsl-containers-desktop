using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Domain.Tests;

[TestClass]
public sealed class ContainerDetailTests
{
    [TestMethod]
    public void ContainerDetail_CreatedWithPortsEnvironmentMountsNetworksAndRunState_StoresValues()
    {
        // Arrange
        var createdAt = new DateTimeOffset(2026, 7, 4, 0, 0, 0, TimeSpan.Zero);
        var startedAt = new DateTimeOffset(2026, 7, 4, 0, 1, 0, TimeSpan.Zero);
        var finishedAt = new DateTimeOffset(2026, 7, 4, 0, 2, 0, TimeSpan.Zero);

        // Act
        var detail = new ContainerDetail(
            Id: "sha256:abc",
            Name: "web",
            Image: "nginx:latest",
            State: ContainerState.Stopped,
            CreatedAt: createdAt,
            Command: "sleep infinity",
            Entrypoint: "/entrypoint.sh",
            Ports: [new ContainerPortMapping("127.0.0.1", 8080, 80, "tcp")],
            Environment: [new ContainerEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development")],
            Mounts: [new ContainerMount("bind", "C:\\data", "/data", IsReadOnly: true)],
            Networks: [new ContainerNetwork("bridge", "172.17.0.2")],
            RunState: new ContainerRunState(startedAt, finishedAt, ExitCode: 0, HealthStatus: "healthy"));

        // Assert
        Assert.AreEqual("sha256:abc", detail.Id);
        Assert.AreEqual("web", detail.Name);
        Assert.AreEqual(createdAt, detail.CreatedAt);
        Assert.AreEqual("sleep infinity", detail.Command);
        Assert.AreEqual("127.0.0.1", detail.Ports[0].HostAddress);
        Assert.AreEqual((ushort)8080, detail.Ports[0].HostPort);
        Assert.AreEqual((ushort)80, detail.Ports[0].ContainerPort);
        Assert.AreEqual("tcp", detail.Ports[0].Protocol);
        Assert.AreEqual("ASPNETCORE_ENVIRONMENT", detail.Environment[0].Name);
        Assert.AreEqual("Development", detail.Environment[0].Value);
        Assert.IsTrue(detail.Mounts[0].IsReadOnly);
        Assert.AreEqual("bridge", detail.Networks[0].Name);
        Assert.AreEqual("172.17.0.2", detail.Networks[0].IpAddress);
        Assert.AreEqual(0, detail.RunState.ExitCode);
        Assert.AreEqual("healthy", detail.RunState.HealthStatus);
    }
}
