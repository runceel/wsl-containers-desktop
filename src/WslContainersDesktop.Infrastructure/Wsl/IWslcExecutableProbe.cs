namespace WslContainersDesktop.Infrastructure.Wsl;

/// <summary>
/// WSL Containers CLI（<c>wslc.exe</c>）が利用可能かどうかを判定する低レベルseam。
/// </summary>
public interface IWslcExecutableProbe
{
    /// <summary>
    /// <c>wslc.exe</c> が実行環境で利用可能かどうかを返す。
    /// </summary>
    bool IsAvailable();
}
