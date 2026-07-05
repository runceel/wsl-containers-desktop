namespace WslContainersDesktop.Domain;

/// <summary>
/// WSL Containersに割り当てるリソース制限。<see langword="null"/> はWSLの既定値を使うことを表す。
/// </summary>
/// <param name="MemoryMegabytes">割り当てメモリ量（MB）。<see langword="null"/> の場合はWSL既定。</param>
/// <param name="ProcessorCount">割り当てる論理プロセッサ数。<see langword="null"/> の場合はWSL既定。</param>
public sealed record WslResourceLimits(int? MemoryMegabytes, int? ProcessorCount)
{
    /// <summary>
    /// すべての値が未指定（WSL既定に委ねる）かどうか。
    /// </summary>
    public bool IsDefault => MemoryMegabytes is null && ProcessorCount is null;

    /// <summary>
    /// 各値が有効（未指定、または正の整数）かどうか。
    /// </summary>
    public bool IsValid => (MemoryMegabytes is null or > 0) && (ProcessorCount is null or > 0);

    /// <summary>
    /// すべての値をWSLの既定に委ねるリソース制限。
    /// </summary>
    public static WslResourceLimits Defaults { get; } = new(null, null);
}
