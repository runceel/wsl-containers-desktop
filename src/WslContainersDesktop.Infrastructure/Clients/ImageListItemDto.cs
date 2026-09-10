using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace WslContainersDesktop.Infrastructure.Clients;

/// <summary>
/// <c>wslc image list --format json --no-trunc</c> の1要素に対応するJSON DTO。
/// </summary>
[
    JsonConverter(typeof(ImageListItemDtoJsonConverter))
]
internal sealed partial class ImageListItemDto
{
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UnixEpoch;

    public string Id { get; private set; } = string.Empty;

    public string Repository { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public string Tag { get; private set; } = string.Empty;

    public static ImageListItemDto Parse(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("イメージ一覧の要素はJSONオブジェクトである必要があります。");
        }

        var item = new ImageListItemDto();
        var hasId = false;
        var hasRepository = false;
        var hasTag = false;
        var hasSize = false;
        var hasCreated = false;
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "Id":
                case "ID":
                    item.Id = GetRequiredString(property);
                    hasId = true;
                    break;
                case "Repository":
                    item.Repository = GetRequiredString(property);
                    hasRepository = true;
                    break;
                case "Tag":
                    item.Tag = GetRequiredString(property);
                    hasTag = true;
                    break;
                case "Created":
                    item.CreatedAt = ParseUnixTimeSeconds(property.Value);
                    hasCreated = true;
                    break;
                case "CreatedAt":
                    item.CreatedAt = ParseDockerTimestamp(property.Value);
                    hasCreated = true;
                    break;
                case "Size":
                    item.SizeBytes = ParseSize(property.Value);
                    hasSize = true;
                    break;
            }
        }

        if (!hasId || !hasRepository || !hasTag || !hasSize || !hasCreated)
        {
            throw new JsonException("イメージ一覧の要素にはID、Repository、Tag、Size、CreatedまたはCreatedAtが必要です。");
        }

        return item;
    }

    private static string GetRequiredString(JsonProperty property)
    {
        if (property.Value.ValueKind != JsonValueKind.String || property.Value.GetString() is not { } value)
        {
            throw new JsonException($"'{property.Name}' は文字列である必要があります。");
        }

        return value;
    }

    private static DateTimeOffset ParseUnixTimeSeconds(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var unixTimeSeconds))
        {
            throw new JsonException("'Created' は整数である必要があります。");
        }

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixTimeSeconds);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new JsonException("'Created' の値が範囲外です。", ex);
        }
    }

    private static DateTimeOffset ParseDockerTimestamp(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String &&
            value.GetString() is { } timestamp &&
            CliDateTimeParsing.TryParseDockerTimestamp(timestamp, out var createdAt))
        {
            return createdAt;
        }

        throw new JsonException("'CreatedAt' の値が不正です。");
    }

    private static long ParseSize(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var legacySize))
        {
            return legacySize;
        }

        if (value.ValueKind == JsonValueKind.String &&
            value.GetString() is { } size &&
            DockerSizeRegex().Match(size) is { Success: true } match &&
            decimal.TryParse(match.Groups["amount"].Value, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount) &&
            DockerUnitMultipliers.TryGetValue(match.Groups["unit"].Value, out var multiplier))
        {
            try
            {
                return decimal.ToInt64(decimal.Round(amount * multiplier, 0, MidpointRounding.AwayFromZero));
            }
            catch (OverflowException ex)
            {
                throw new JsonException("'Size' の値が範囲外です。", ex);
            }
        }

        throw new JsonException("'Size' の値が不正です。");
    }

    private static readonly IReadOnlyDictionary<string, decimal> DockerUnitMultipliers = new Dictionary<string, decimal>
    {
        ["B"] = 1,
        ["kB"] = 1_000,
        ["MB"] = 1_000_000,
        ["GB"] = 1_000_000_000,
        ["TB"] = 1_000_000_000_000,
        ["PB"] = 1_000_000_000_000_000,
    };

    [GeneratedRegex(@"^(?<amount>\d+(?:\.\d+)?)\s*(?<unit>[A-Za-z]+)$")]
    private static partial Regex DockerSizeRegex();

}

internal sealed class ImageListItemDtoJsonConverter : JsonConverter<ImageListItemDto>
{
    public override bool HandleNull => true;

    public override ImageListItemDto Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return ImageListItemDto.Parse(document.RootElement);
    }

    public override void Write(Utf8JsonWriter writer, ImageListItemDto value, JsonSerializerOptions options)
    {
        throw new NotSupportedException();
    }
}
