using System.Reflection;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Application.Tests.Ports;

// Architecture fitness test for ADR-0017: the focused runtime ports should be exposed from the application assembly.
[TestClass]
public sealed class RuntimePortBoundaryTests
{
    [TestMethod]
    public void FocusedRuntimePorts_ApplicationAssembly_ExposeExpectedContracts()
    {
        // Arrange
        var applicationAssembly = typeof(IContainerManagementService).Assembly;
        var expectedContracts = new[]
        {
            new ExpectedContract(
                "WslContainersDesktop.Application.Ports.IContainerQueryClient",
                ["ListContainersAsync", "GetContainerDetailAsync"],
                [
                    new ExpectedMethodSignature("ListContainersAsync", typeof(Task<IReadOnlyList<Container>>), [typeof(CancellationToken)], [true]),
                    new ExpectedMethodSignature("GetContainerDetailAsync", typeof(Task<ContainerDetail>), [typeof(string), typeof(CancellationToken)], [false, true])
                ]),
            new ExpectedContract(
                "WslContainersDesktop.Application.Ports.IContainerLifecycleClient",
                ["RunContainerAsync", "StartAsync", "StopAsync", "DeleteAsync"],
                [
                    new ExpectedMethodSignature("RunContainerAsync", typeof(Task), [typeof(ContainerRunRequest), typeof(CancellationToken)], [false, true]),
                    new ExpectedMethodSignature("StartAsync", typeof(Task), [typeof(string), typeof(CancellationToken)], [false, true]),
                    new ExpectedMethodSignature("StopAsync", typeof(Task), [typeof(string), typeof(CancellationToken)], [false, true]),
                    new ExpectedMethodSignature("DeleteAsync", typeof(Task), [typeof(string), typeof(CancellationToken)], [false, true])
                ]),
            new ExpectedContract(
                "WslContainersDesktop.Application.Ports.IContainerLogClient",
                ["GetContainerLogsAsync", "FollowContainerLogsAsync"],
                [
                    new ExpectedMethodSignature("GetContainerLogsAsync", typeof(Task<IReadOnlyList<string>>), [typeof(string), typeof(CancellationToken)], [false, true]),
                    new ExpectedMethodSignature("FollowContainerLogsAsync", typeof(IAsyncEnumerable<string>), [typeof(string), typeof(CancellationToken)], [false, true])
                ]),
            new ExpectedContract(
                "WslContainersDesktop.Application.Ports.IContainerExecClient",
                ["OpenExecSessionAsync"],
                [
                    new ExpectedMethodSignature("OpenExecSessionAsync", typeof(Task<IContainerExecSession>), [typeof(string), typeof(CancellationToken)], [false, true])
                ]),
            new ExpectedContract(
                "WslContainersDesktop.Application.Ports.IContainerStatsClient",
                ["GetContainerStatsAsync"],
                [
                    new ExpectedMethodSignature("GetContainerStatsAsync", typeof(Task<IReadOnlyList<ContainerResourceUsage>>), [typeof(CancellationToken)], [true])
                ]),
            new ExpectedContract(
                "WslContainersDesktop.Application.Ports.IImageRuntimeClient",
                ["ListImagesAsync", "PullImageAsync", "DeleteImageAsync"],
                [
                    new ExpectedMethodSignature("ListImagesAsync", typeof(Task<IReadOnlyList<ContainerImage>>), [typeof(CancellationToken)], [true]),
                    new ExpectedMethodSignature("PullImageAsync", typeof(Task), [typeof(string), typeof(CancellationToken)], [false, true]),
                    new ExpectedMethodSignature("DeleteImageAsync", typeof(Task), [typeof(string), typeof(CancellationToken)], [false, true])
                ]),
            new ExpectedContract(
                "WslContainersDesktop.Application.Ports.IVolumeRuntimeClient",
                ["ListVolumesAsync", "CreateVolumeAsync", "DeleteVolumeAsync"],
                [
                    new ExpectedMethodSignature("ListVolumesAsync", typeof(Task<IReadOnlyList<ContainerVolume>>), [typeof(CancellationToken)], [true]),
                    new ExpectedMethodSignature("CreateVolumeAsync", typeof(Task), [typeof(string), typeof(CancellationToken)], [false, true]),
                    new ExpectedMethodSignature("DeleteVolumeAsync", typeof(Task), [typeof(string), typeof(CancellationToken)], [false, true])
                ]),
            new ExpectedContract(
                "WslContainersDesktop.Application.Ports.INetworkRuntimeClient",
                ["ListNetworksAsync", "CreateNetworkAsync", "DeleteNetworkAsync"],
                [
                    new ExpectedMethodSignature("ListNetworksAsync", typeof(Task<IReadOnlyList<ContainerNetworkResource>>), [typeof(CancellationToken)], [true]),
                    new ExpectedMethodSignature("CreateNetworkAsync", typeof(Task), [typeof(string), typeof(CancellationToken)], [false, true]),
                    new ExpectedMethodSignature("DeleteNetworkAsync", typeof(Task), [typeof(string), typeof(CancellationToken)], [false, true])
                ])
        };

        // Act
        foreach (var expectedContract in expectedContracts)
        {
            var interfaceType = applicationAssembly.GetType(expectedContract.FullName);

            // Assert
            Assert.IsNotNull(interfaceType, $"Expected interface '{expectedContract.FullName}' to exist in the application assembly.");
            Assert.AreEqual(expectedContract.FullName, interfaceType!.FullName, $"Expected the reflected type name to match '{expectedContract.FullName}'.");

            var declaredMethodNames = interfaceType
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(method => method.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            var expectedMethodNames = expectedContract.ExpectedMethodNames
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            CollectionAssert.AreEqual(expectedMethodNames, declaredMethodNames, $"Expected methods on '{expectedContract.FullName}' to match the ADR-0017 contract.");

            foreach (var expectedMethodSignature in expectedContract.ExpectedMethodSignatures)
            {
                var method = interfaceType.GetMethod(
                    expectedMethodSignature.Name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly,
                    binder: null,
                    types: expectedMethodSignature.ParameterTypes,
                    modifiers: null);

                Assert.IsNotNull(method, $"Expected method '{expectedMethodSignature.Name}' on '{expectedContract.FullName}' to exist with the expected signature.");
                Assert.AreEqual(expectedMethodSignature.ReturnType, method!.ReturnType, $"Expected return type for '{expectedContract.FullName}.{expectedMethodSignature.Name}' to match the ADR-0017 contract.");

                var parameters = method.GetParameters();
                Assert.AreEqual(expectedMethodSignature.ParameterTypes.Length, parameters.Length, $"Expected parameter count for '{expectedContract.FullName}.{expectedMethodSignature.Name}' to match the ADR-0017 contract.");

                for (var index = 0; index < parameters.Length; index++)
                {
                    Assert.AreEqual(expectedMethodSignature.ParameterTypes[index], parameters[index].ParameterType, $"Expected parameter {index + 1} type for '{expectedContract.FullName}.{expectedMethodSignature.Name}' to match the ADR-0017 contract.");
                    Assert.AreEqual(expectedMethodSignature.ParameterIsOptional[index], parameters[index].HasDefaultValue, $"Expected parameter {index + 1} optionality for '{expectedContract.FullName}.{expectedMethodSignature.Name}' to match the ADR-0017 contract.");
                }
            }
        }
    }

    private sealed record ExpectedContract(
        string FullName,
        string[] ExpectedMethodNames,
        ExpectedMethodSignature[] ExpectedMethodSignatures);

    private sealed record ExpectedMethodSignature(
        string Name,
        Type ReturnType,
        Type[] ParameterTypes,
        bool[] ParameterIsOptional);
}
