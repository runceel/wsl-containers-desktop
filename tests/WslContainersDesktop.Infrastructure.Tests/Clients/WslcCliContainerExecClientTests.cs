using System.Runtime.CompilerServices;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Infrastructure.Clients;
using WslContainersDesktop.Infrastructure.Tests.Fakes;

namespace WslContainersDesktop.Infrastructure.Tests.Clients;

[TestClass]
public sealed class WslcCliContainerExecClientTests
{
    [TestMethod]
    public async Task OpenExecSessionAsync_CliArguments_AreContainerExecInteractiveNoTtyShell()
    {
        // Arrange
        var expected = new FakeContainerExecSession();
        var runner = new FakeWslcCliRunner { ExecSession = expected };
        var sut = new WslcCliContainerExecClient(runner);

        // Act
        var session = await sut.OpenExecSessionAsync("c1");

        // Assert
        Assert.AreSame(expected, session);
        CollectionAssert.AreEqual(new[] { "container", "exec", "-i", "c1", "/bin/sh" }, runner.OpenInteractiveCalls[0].ToList());
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
