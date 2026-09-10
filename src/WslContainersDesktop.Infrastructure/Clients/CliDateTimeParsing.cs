using System.Globalization;
using System.Text.RegularExpressions;

namespace WslContainersDesktop.Infrastructure.Clients;

/// <summary>
/// <c>wslc</c> CLIが返す日時文字列を解析する共通ユーティリティ。
/// コンテナ詳細・ボリューム・ネットワークの検査結果で共通して使われる。
/// </summary>
internal static partial class CliDateTimeParsing
{
    /// <summary>
    /// 日時文字列を解析する。解析できない場合は <see cref="DateTimeOffset.MinValue"/> を返す。
    /// </summary>
    public static DateTimeOffset ParseDateTimeOffsetOrDefault(string value)
    {
        return ParseNullableDateTimeOffset(value) ?? DateTimeOffset.MinValue;
    }

    /// <summary>
    /// 日時文字列を解析する。空文字列・既定値（<c>0001-01-01</c>）・解析失敗の場合は
    /// <c>null</c> を返す。
    /// </summary>
    public static DateTimeOffset? ParseNullableDateTimeOffset(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith("0001-01-01", StringComparison.Ordinal))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var result)
            ? result.ToUniversalTime()
            : null;
    }

    /// <summary>
    /// Docker CLI形式の日時文字列をUTCへ正規化して解析する。
    /// </summary>
    public static bool TryParseDockerTimestamp(string value, out DateTimeOffset result)
    {
        if (DockerTimestampRegex().Match(value) is { Success: true } match)
        {
            var offset = match.Groups["offset"].Value.Insert(3, ":");
            var normalizedTimestamp = $"{match.Groups["dateTime"].Value} {offset}";
            if (DateTimeOffset.TryParseExact(
                normalizedTimestamp,
                "yyyy-MM-dd HH:mm:ss.FFFFFFF zzz",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out result))
            {
                result = result.ToUniversalTime();
                return true;
            }
        }

        result = default;
        return false;
    }

    [GeneratedRegex(@"^(?<dateTime>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}(?:\.\d{1,7})?) (?<offset>[+-]\d{4})(?:\s+\S+)?$")]
    private static partial Regex DockerTimestampRegex();
}
