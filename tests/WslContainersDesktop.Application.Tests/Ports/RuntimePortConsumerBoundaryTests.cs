using System.Reflection;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Application.Services;

namespace WslContainersDesktop.Application.Tests.Ports;

// Architecture fitness test for ADR-0017: application services should depend on focused runtime ports rather than the legacy broad runtime port.
[TestClass]
public sealed class RuntimePortConsumerBoundaryTests
{
    [TestMethod]
    public void ApplicationServices_Constructors_UseOnlyRequiredFocusedRuntimePorts()
    {
        // Arrange
        var serviceConstructorExpectations = GetServiceConstructorExpectations();

        // Act
        foreach (var expectation in serviceConstructorExpectations)
        {
            var constructors = expectation.ServiceType
                .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            var matchingConstructor = constructors.FirstOrDefault(constructor =>
            {
                var actualParameterTypeFullNames = constructor
                    .GetParameters()
                    .Select(parameter => parameter.ParameterType.FullName!)
                    .ToArray();

                return actualParameterTypeFullNames.SequenceEqual(expectation.ExpectedParameterTypeFullNames, StringComparer.Ordinal);
            });

            // Assert
            Assert.IsNotNull(matchingConstructor, $"Expected constructor parameters for '{expectation.ServiceType.FullName}' to match the ADR-0017 focused runtime-port contract.");
        }
    }

    [TestMethod]
    public void ApplicationServices_Constructors_ExposeOnlyFocusedRuntimePorts()
    {
        // Arrange
        var serviceConstructorExpectations = GetServiceConstructorExpectations();

        // Act
        foreach (var expectation in serviceConstructorExpectations)
        {
            var constructors = expectation.ServiceType
                .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

            // Assert
            Assert.AreEqual(
                1,
                constructors.Length,
                $"Expected '{expectation.ServiceType.FullName}' to expose exactly one declared public constructor.");

            var actualParameterTypeFullNames = constructors[0]
                .GetParameters()
                .Select(parameter => parameter.ParameterType.FullName!)
                .ToArray();

            CollectionAssert.AreEqual(
                expectation.ExpectedParameterTypeFullNames,
                actualParameterTypeFullNames,
                $"Expected constructor parameters for '{expectation.ServiceType.FullName}' to match the ADR-0017 focused runtime-port contract.");
        }
    }

    [TestMethod]
    public void LegacyContainerRuntimePort_ApplicationAssembly_IsAbsent()
    {
        // Arrange
        var applicationAssembly = typeof(ContainerManagementService).Assembly;

        // Act
        var legacyRuntimePortType = applicationAssembly.GetType("WslContainersDesktop.Application.Ports.IContainerRuntimeClient");

        // Assert
        Assert.IsNull(legacyRuntimePortType, "Expected the legacy broad runtime port to be absent from the application assembly.");
    }

    private static ServiceConstructorExpectation[] GetServiceConstructorExpectations()
    {
        return
        [
            new ServiceConstructorExpectation(
                typeof(ContainerManagementService),
                [
                    "WslContainersDesktop.Application.Ports.IContainerQueryClient",
                    "WslContainersDesktop.Application.Ports.IContainerLifecycleClient",
                    "WslContainersDesktop.Application.Ports.IContainerLogClient",
                    "WslContainersDesktop.Application.Ports.IContainerExecClient",
                    "WslContainersDesktop.Application.Ports.IContainerStatsClient"
                ]),
            new ServiceConstructorExpectation(
                typeof(ImageManagementService),
                [
                    "WslContainersDesktop.Application.Ports.IImageRuntimeClient"
                ]),
            new ServiceConstructorExpectation(
                typeof(VolumeManagementService),
                [
                    "WslContainersDesktop.Application.Ports.IVolumeRuntimeClient",
                    "WslContainersDesktop.Application.Ports.IContainerQueryClient"
                ]),
            new ServiceConstructorExpectation(
                typeof(NetworkManagementService),
                [
                    "WslContainersDesktop.Application.Ports.INetworkRuntimeClient",
                    "WslContainersDesktop.Application.Ports.IContainerQueryClient"
                ]),
            new ServiceConstructorExpectation(
                typeof(DashboardService),
                [
                    "WslContainersDesktop.Application.Ports.IContainerQueryClient",
                    "WslContainersDesktop.Application.Ports.IImageRuntimeClient",
                    "WslContainersDesktop.Application.Ports.IVolumeRuntimeClient",
                    "WslContainersDesktop.Application.Ports.INetworkRuntimeClient",
                    "WslContainersDesktop.Application.Ports.IContainerStatsClient"
                ])
        ];
    }

    private sealed record ServiceConstructorExpectation(Type ServiceType, string[] ExpectedParameterTypeFullNames);
}
