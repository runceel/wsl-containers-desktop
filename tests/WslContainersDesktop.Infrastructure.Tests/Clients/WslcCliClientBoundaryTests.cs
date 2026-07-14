using System.Reflection;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Infrastructure.Clients;

namespace WslContainersDesktop.Infrastructure.Tests.Clients;

// Architecture fitness test for ADR-0017: focused Wslc CLI clients in the infrastructure assembly should each implement a single matching runtime port.
[TestClass]
public sealed class WslcCliClientBoundaryTests
{
    [TestMethod]
    public void FocusedWslcCliClients_InfrastructureAssembly_ImplementSingleMatchingRuntimePort()
    {
        // Arrange
        var infrastructureAssembly = typeof(WslcCliContainerQueryClient).Assembly;
        var focusedPortTypes = new[]
        {
            typeof(IContainerQueryClient),
            typeof(IContainerLifecycleClient),
            typeof(IContainerLogClient),
            typeof(IContainerExecClient),
            typeof(IContainerStatsClient),
            typeof(IImageRuntimeClient),
            typeof(IVolumeRuntimeClient),
            typeof(INetworkRuntimeClient)
        };

        var expectedClientPortMappings = new[]
        {
            new ExpectedClientPortMapping("WslContainersDesktop.Infrastructure.Clients.WslcCliContainerQueryClient", typeof(IContainerQueryClient)),
            new ExpectedClientPortMapping("WslContainersDesktop.Infrastructure.Clients.WslcCliContainerLifecycleClient", typeof(IContainerLifecycleClient)),
            new ExpectedClientPortMapping("WslContainersDesktop.Infrastructure.Clients.WslcCliContainerLogClient", typeof(IContainerLogClient)),
            new ExpectedClientPortMapping("WslContainersDesktop.Infrastructure.Clients.WslcCliContainerExecClient", typeof(IContainerExecClient)),
            new ExpectedClientPortMapping("WslContainersDesktop.Infrastructure.Clients.WslcCliContainerStatsClient", typeof(IContainerStatsClient)),
            new ExpectedClientPortMapping("WslContainersDesktop.Infrastructure.Clients.WslcCliImageRuntimeClient", typeof(IImageRuntimeClient)),
            new ExpectedClientPortMapping("WslContainersDesktop.Infrastructure.Clients.WslcCliVolumeRuntimeClient", typeof(IVolumeRuntimeClient)),
            new ExpectedClientPortMapping("WslContainersDesktop.Infrastructure.Clients.WslcCliNetworkRuntimeClient", typeof(INetworkRuntimeClient))
        };

        // Act
        foreach (var mapping in expectedClientPortMappings)
        {
            var clientType = infrastructureAssembly.GetType(mapping.FullName);

            // Assert
            Assert.IsNotNull(clientType, $"Expected client type '{mapping.FullName}' to exist in the infrastructure assembly.");
            Assert.IsTrue(clientType!.IsPublic, $"Expected client type '{mapping.FullName}' to be public.");
            Assert.IsTrue(clientType.IsSealed, $"Expected client type '{mapping.FullName}' to be sealed.");

            var implementedFocusedPorts = clientType
                .GetInterfaces()
                .Where(interfaceType => focusedPortTypes.Contains(interfaceType))
                .OrderBy(interfaceType => interfaceType.FullName, StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEqual(
                new[] { mapping.ExpectedPortType },
                implementedFocusedPorts,
                $"Expected client type '{mapping.FullName}' to implement exactly the focused runtime port '{mapping.ExpectedPortType.FullName}'.");
        }
    }

    [TestMethod]
    public void LegacyContainerRuntimeClient_InfrastructureAssembly_IsAbsent()
    {
        // Arrange
        var infrastructureAssembly = typeof(WslcCliContainerQueryClient).Assembly;

        // Act
        var legacyRuntimeClientType = infrastructureAssembly.GetType("WslContainersDesktop.Infrastructure.Clients.WslcCliContainerRuntimeClient");

        // Assert
        Assert.IsNull(legacyRuntimeClientType, "Expected the legacy broad runtime client to be absent from the infrastructure assembly.");
    }

    private sealed record ExpectedClientPortMapping(string FullName, Type ExpectedPortType);
}
