using WslContainersDesktop.Application.Exceptions;
using WslContainersDesktop.Infrastructure.Cli;
using WslContainersDesktop.Infrastructure.Clients;
using WslContainersDesktop.Infrastructure.Tests.Fakes;

namespace WslContainersDesktop.Infrastructure.Tests.Clients;

[TestClass]
public sealed class NetworkManagementInfrastructureTests
{
    [TestMethod]
    public async Task ListNetworksAsync_CliReturnsNetworkAndInspectWithoutCreatedAt_MapsJsonToContainerNetworkWithMinValueAndUserNetworkIsNotSystem()
    {
        // Arrange
        const string listJson = "[{\"Driver\":\"bridge\",\"Id\":\"abc\",\"Name\":\"app-net\"}]";
        const string inspectJson = "[{\"Driver\":\"bridge\",\"Id\":\"abc\",\"IPAM\":{},\"Internal\":false,\"Labels\":{},\"Name\":\"app-net\",\"Scope\":\"local\"}]";
        var runner = new FakeWslcCliRunner();
        runner.RunAsyncFunc = (arguments, cancellationToken) =>
        {
            if (arguments.SequenceEqual(new[] { "network", "list", "--format", "json" }))
            {
                return Task.FromResult(new CliResult(0, listJson, string.Empty));
            }

            if (arguments.SequenceEqual(new[] { "network", "inspect", "app-net" }))
            {
                return Task.FromResult(new CliResult(0, inspectJson, string.Empty));
            }

            return Task.FromResult(new CliResult(0, string.Empty, string.Empty));
        };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        var networks = await sut.ListNetworksAsync();

        // Assert
        Assert.HasCount(1, networks);
        Assert.AreEqual("app-net", networks[0].Name);
        Assert.AreEqual("bridge", networks[0].Driver);
        Assert.AreEqual(DateTimeOffset.MinValue, networks[0].CreatedAt);
        Assert.IsFalse(networks[0].IsSystem);
    }

    [TestMethod]
    public async Task ListNetworksAsync_CliArguments_AreNetworkListJsonThenNetworkInspectForEachName()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, "[]", string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        await sut.ListNetworksAsync();

        // Assert
        CollectionAssert.AreEqual(new[] { "network", "list", "--format", "json" }, runner.Calls[0].ToList());
    }

    [TestMethod]
    public async Task ListNetworksAsync_ListMalformedJson_ThrowsContainerRuntimeExceptionWithInnerException()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, "not-json", string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act & Assert
        var ex = await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(() => sut.ListNetworksAsync());
        Assert.IsNotNull(ex.InnerException);
    }

    [TestMethod]
    public async Task ListNetworksAsync_InspectMalformedJson_ThrowsContainerRuntimeExceptionWithInnerException()
    {
        // Arrange
        var runner = new FakeWslcCliRunner();
        runner.RunAsyncFunc = (arguments, cancellationToken) =>
        {
            if (arguments.SequenceEqual(new[] { "network", "list", "--format", "json" }))
            {
                return Task.FromResult(new CliResult(0, "[{\"Driver\":\"bridge\",\"Name\":\"app-net\"}]", string.Empty));
            }

            return Task.FromResult(new CliResult(0, "not-json", string.Empty));
        };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act & Assert
        var ex = await Assert.ThrowsExactlyAsync<ContainerRuntimeException>(() => sut.ListNetworksAsync());
        Assert.IsNotNull(ex.InnerException);
    }

    [TestMethod]
    public async Task ListNetworksAsync_InspectReturnsEmptyArray_UsesMinValueAndKeepsNetwork()
    {
        // Arrange
        var runner = new FakeWslcCliRunner();
        runner.RunAsyncFunc = (arguments, cancellationToken) =>
        {
            if (arguments.SequenceEqual(new[] { "network", "list", "--format", "json" }))
            {
                return Task.FromResult(new CliResult(0, "[{\"Driver\":\"bridge\",\"Name\":\"app-net\"}]", string.Empty));
            }

            return Task.FromResult(new CliResult(0, "[]", string.Empty));
        };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        var networks = await sut.ListNetworksAsync();

        // Assert
        Assert.HasCount(1, networks);
        Assert.AreEqual(DateTimeOffset.MinValue, networks[0].CreatedAt);
        Assert.AreEqual("app-net", networks[0].Name);
    }

    [TestMethod]
    public async Task ListNetworksAsync_InspectsInBoundedParallelismAndPreservesListOrder()
    {
        // Arrange
        const int maxDegree = 4;
        var names = new[] { "net-1", "net-2", "net-3", "net-4", "net-5" };
        var listJson = $"[{string.Join(",", names.Select(name => $"{{\"Driver\":\"bridge\",\"Name\":\"{name}\"}}"))}]";
        var releaseGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var startedInspectCalls = 0;
        var activeInspectCalls = 0;
        var maxObservedConcurrentCalls = 0;
        async Task<CliResult> WaitForReleaseAsync(string name, CancellationToken cancellationToken)
        {
            await releaseGate.Task.WaitAsync(cancellationToken);
            Interlocked.Decrement(ref activeInspectCalls);
            return new CliResult(0, $"[{{\"CreatedAt\":\"2026-07-02T09:00:00Z\",\"Driver\":\"bridge\",\"Name\":\"{name}\"}}]", string.Empty);
        }
        var runner = new FakeWslcCliRunner();
        runner.RunAsyncFunc = (arguments, cancellationToken) =>
        {
            if (arguments.SequenceEqual(new[] { "network", "list", "--format", "json" }))
            {
                return Task.FromResult(new CliResult(0, listJson, string.Empty));
            }

            if (arguments.Count >= 3 && arguments[0] == "network" && arguments[1] == "inspect")
            {
                var name = arguments[2];
                var current = Interlocked.Increment(ref activeInspectCalls);
                var maxObserved = Math.Max(Volatile.Read(ref maxObservedConcurrentCalls), current);
                Interlocked.Exchange(ref maxObservedConcurrentCalls, maxObserved);
                Interlocked.Increment(ref startedInspectCalls);
                return WaitForReleaseAsync(name, cancellationToken);
            }

            return Task.FromResult(new CliResult(0, string.Empty, string.Empty));
        };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        var task = sut.ListNetworksAsync();
        Assert.IsTrue(SpinWait.SpinUntil(() => Volatile.Read(ref startedInspectCalls) >= maxDegree, TimeSpan.FromSeconds(2)));
        Assert.AreEqual(maxDegree, Volatile.Read(ref maxObservedConcurrentCalls));
        releaseGate.SetResult(true);
        var networks = await task;

        // Assert
        CollectionAssert.AreEqual(names, networks.Select(network => network.Name).ToList());
    }

    [TestMethod]
    public async Task CreateNetworkAsync_CliArguments_AreNetworkCreateWithName()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, string.Empty, string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        await sut.CreateNetworkAsync("app-net");

        // Assert
        CollectionAssert.AreEqual(new[] { "network", "create", "app-net" }, runner.Calls[0].ToList());
    }

    [TestMethod]
    public async Task DeleteNetworkAsync_CliArguments_AreNetworkRemoveWithoutForce()
    {
        // Arrange
        var runner = new FakeWslcCliRunner { Result = new(0, string.Empty, string.Empty) };
        var sut = new WslcCliContainerRuntimeClient(runner);

        // Act
        await sut.DeleteNetworkAsync("app-net");

        // Assert
        var arguments = runner.Calls[0].ToList();
        CollectionAssert.AreEqual(new[] { "network", "remove", "app-net" }, arguments);
        CollectionAssert.DoesNotContain(arguments, "--force");
    }
}
