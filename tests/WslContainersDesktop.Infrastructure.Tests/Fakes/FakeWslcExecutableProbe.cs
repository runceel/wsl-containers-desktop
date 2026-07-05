using WslContainersDesktop.Infrastructure.Wsl;

namespace WslContainersDesktop.Infrastructure.Tests.Fakes;

/// <summary>
/// <see cref="IWslcExecutableProbe"/> のテスト用フェイク実装。
/// </summary>
internal sealed class FakeWslcExecutableProbe : IWslcExecutableProbe
{
    public bool Available { get; set; }

    public bool IsAvailable() => Available;
}
