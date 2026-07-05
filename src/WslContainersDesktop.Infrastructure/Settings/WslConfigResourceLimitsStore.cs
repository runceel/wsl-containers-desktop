using System.Globalization;
using System.Text.RegularExpressions;
using WslContainersDesktop.Application.Exceptions;
using WslContainersDesktop.Application.Ports;
using WslContainersDesktop.Domain;

namespace WslContainersDesktop.Infrastructure.Settings;

/// <summary>
/// <c>.wslconfig</c> の <c>[wsl2]</c> セクションに対してリソース制限
/// （<c>memory</c> / <c>processors</c>）を読み書きする <see cref="IWslResourceLimitsStore"/> の実装。
/// 書き込み時は他セクション・他キー・コメント・改行を保全し、リソースキーのみを更新する。
/// </summary>
public sealed class WslConfigResourceLimitsStore(IWslConfigFileAccessor fileAccessor) : IWslResourceLimitsStore
{
    private const string Wsl2SectionName = "wsl2";
    private const string MemoryKey = "memory";
    private const string ProcessorsKey = "processors";

    private static readonly Regex MemoryValueRegex =
        new(@"^(?<number>\d+)\s*(?<unit>[a-zA-Z]+)?$", RegexOptions.Compiled);

    /// <inheritdoc />
    public async Task<WslResourceLimits> GetAsync(CancellationToken cancellationToken = default)
    {
        string? content;
        try
        {
            content = await fileAccessor.ReadAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not WslSettingsAccessException)
        {
            throw new WslSettingsAccessException(".wslconfig の読み取りに失敗しました。", ex);
        }

        if (string.IsNullOrEmpty(content))
        {
            return WslResourceLimits.Defaults;
        }

        int? memoryMegabytes = null;
        int? processorCount = null;
        var inWsl2Section = false;

        foreach (var rawLine in SplitLines(content))
        {
            var line = rawLine.Trim();
            if (IsBlankOrComment(line))
            {
                continue;
            }

            if (TryGetSectionName(line, out var sectionName))
            {
                inWsl2Section = string.Equals(sectionName, Wsl2SectionName, StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (!inWsl2Section || !TryGetKeyValue(line, out var key, out var value) || value.Length == 0)
            {
                continue;
            }

            if (string.Equals(key, MemoryKey, StringComparison.OrdinalIgnoreCase))
            {
                memoryMegabytes = ParseMemoryMegabytes(value);
            }
            else if (string.Equals(key, ProcessorsKey, StringComparison.OrdinalIgnoreCase))
            {
                processorCount = ParseProcessorCount(value);
            }
        }

        return new WslResourceLimits(memoryMegabytes, processorCount);
    }

    /// <inheritdoc />
    public async Task SaveAsync(WslResourceLimits limits, CancellationToken cancellationToken = default)
    {
        try
        {
            var content = await fileAccessor.ReadAsync(cancellationToken);
            var updated = ApplyLimits(content, limits);
            await fileAccessor.WriteAsync(updated, cancellationToken);
        }
        catch (Exception ex) when (ex is not WslSettingsAccessException)
        {
            throw new WslSettingsAccessException(".wslconfig の書き込みに失敗しました。", ex);
        }
    }

    private static string ApplyLimits(string? content, WslResourceLimits limits)
    {
        var newline = DetectNewline(content);
        var keyLines = BuildResourceKeyLines(limits);

        if (string.IsNullOrEmpty(content))
        {
            if (keyLines.Count == 0)
            {
                return string.Empty;
            }

            var freshLines = new List<string> { $"[{Wsl2SectionName}]" };
            freshLines.AddRange(keyLines);
            return string.Join(newline, freshLines) + newline;
        }

        var lines = content.Split(["\r\n", "\n"], StringSplitOptions.None);
        var result = new List<string>(lines.Length + keyLines.Count + 1);
        var inWsl2Section = false;
        var insertedIntoFirstWsl2 = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (TryGetSectionName(line, out var sectionName))
            {
                var isWsl2 = string.Equals(sectionName, Wsl2SectionName, StringComparison.OrdinalIgnoreCase);
                result.Add(rawLine);
                inWsl2Section = isWsl2;
                if (isWsl2 && !insertedIntoFirstWsl2)
                {
                    result.AddRange(keyLines);
                    insertedIntoFirstWsl2 = true;
                }

                continue;
            }

            if (inWsl2Section && IsResourceKeyLine(line))
            {
                continue;
            }

            result.Add(rawLine);
        }

        if (!insertedIntoFirstWsl2 && keyLines.Count > 0)
        {
            result.Add($"[{Wsl2SectionName}]");
            result.AddRange(keyLines);
        }

        return string.Join(newline, result);
    }

    private static List<string> BuildResourceKeyLines(WslResourceLimits limits)
    {
        var keyLines = new List<string>(2);
        if (limits.MemoryMegabytes is int memory)
        {
            keyLines.Add($"{MemoryKey}={memory.ToString(CultureInfo.InvariantCulture)}MB");
        }

        if (limits.ProcessorCount is int processors)
        {
            keyLines.Add($"{ProcessorsKey}={processors.ToString(CultureInfo.InvariantCulture)}");
        }

        return keyLines;
    }

    private static bool IsResourceKeyLine(string trimmedLine)
    {
        if (IsBlankOrComment(trimmedLine) || !TryGetKeyValue(trimmedLine, out var key, out _))
        {
            return false;
        }

        return string.Equals(key, MemoryKey, StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, ProcessorsKey, StringComparison.OrdinalIgnoreCase);
    }

    private static string DetectNewline(string? content)
    {
        if (content is not null && !content.Contains("\r\n", StringComparison.Ordinal)
            && content.Contains('\n', StringComparison.Ordinal))
        {
            return "\n";
        }

        return "\r\n";
    }

    private static string[] SplitLines(string content) => content.Split(["\r\n", "\n"], StringSplitOptions.None);

    private static bool IsBlankOrComment(string trimmedLine)
        => trimmedLine.Length == 0 || trimmedLine[0] == '#' || trimmedLine[0] == ';';

    private static bool TryGetSectionName(string trimmedLine, out string sectionName)
    {
        if (trimmedLine.Length >= 2 && trimmedLine[0] == '[' && trimmedLine[^1] == ']')
        {
            sectionName = trimmedLine[1..^1].Trim();
            return true;
        }

        sectionName = string.Empty;
        return false;
    }

    private static bool TryGetKeyValue(string trimmedLine, out string key, out string value)
    {
        var equalsIndex = trimmedLine.IndexOf('=');
        if (equalsIndex <= 0)
        {
            key = string.Empty;
            value = string.Empty;
            return false;
        }

        key = trimmedLine[..equalsIndex].Trim();
        value = trimmedLine[(equalsIndex + 1)..].Trim();
        return key.Length > 0;
    }

    private static int ParseMemoryMegabytes(string value)
    {
        var match = MemoryValueRegex.Match(value);
        if (!match.Success
            || !long.TryParse(match.Groups["number"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
        {
            throw new WslSettingsAccessException($".wslconfig の memory 値 '{value}' を解釈できません。");
        }

        long megabytes;
        try
        {
            megabytes = checked(match.Groups["unit"].Value.ToLowerInvariant() switch
            {
                "" or "mb" => number,
                "gb" => number * 1024L,
                "kb" => number / 1024L,
                "tb" => number * 1024L * 1024L,
                _ => throw new WslSettingsAccessException($".wslconfig の memory 値 '{value}' を解釈できません。"),
            });
        }
        catch (OverflowException ex)
        {
            throw new WslSettingsAccessException($".wslconfig の memory 値 '{value}' を解釈できません。", ex);
        }

        if (megabytes is <= 0 or > int.MaxValue)
        {
            throw new WslSettingsAccessException($".wslconfig の memory 値 '{value}' を解釈できません。");
        }

        return (int)megabytes;
    }

    private static int ParseProcessorCount(string value)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
        {
            throw new WslSettingsAccessException($".wslconfig の processors 値 '{value}' を解釈できません。");
        }

        return count;
    }
}
