using System.Globalization;

namespace WslContainersDesktop.Infrastructure.Clients;

/// <summary>
/// <c>wslc</c> CLIが返す日時文字列を解析する共通ユーティリティ。
/// コンテナ詳細・ボリューム・ネットワークの検査結果で共通して使われる。
/// </summary>
internal static class CliDateTimeParsing
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
}
