using System.Text.Json;
using System.Text.Json.Serialization;

namespace WslContainersDesktop.Infrastructure.Clients;

/// <summary>
/// <c>wslc list -a --format json</c> の1要素に対応するJSON DTO。
/// </summary>
[JsonConverter(typeof(ContainerListItemDtoJsonConverter))]
internal sealed class ContainerListItemDto
{
    public string Id { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Image { get; private set; } = string.Empty;

    /// <summary>
    /// wslc SDKの <c>ContainerState</c> 列挙体の数値
    /// （Invalid=0, Created=1, Running=2, Exited=3, Deleted=4）。
    /// </summary>
    public int State { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UnixEpoch;

    public static ContainerListItemDto Parse(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("コンテナ一覧の要素はJSONオブジェクトである必要があります。");
        }

        var item = new ContainerListItemDto();
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "Id":
                case "ID":
                    item.Id = GetRequiredString(property);
                    break;
                case "Name":
                case "Names":
                    item.Name = GetRequiredString(property);
                    break;
                case "Image":
                    item.Image = GetRequiredString(property);
                    break;
                case "State":
                    item.State = ParseState(property.Value);
                    break;
                case "CreatedAt":
                    item.CreatedAt = ParseCreatedAt(property.Value);
                    break;
            }
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

    private static int ParseState(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var legacyState))
        {
            return legacyState;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString() switch
            {
                "running" => 2,
                "created" or "paused" or "restarting" or "removing" or "exited" or "dead" => 3,
                _ => throw new JsonException("'State' の値が不正です。"),
            };
        }

        throw new JsonException("'State' は数値または文字列である必要があります。");
    }

    private static DateTimeOffset ParseCreatedAt(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var unixTimeSeconds))
        {
            try
            {
                return DateTimeOffset.FromUnixTimeSeconds(unixTimeSeconds);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                throw new JsonException("'CreatedAt' の値が範囲外です。", ex);
            }
        }

        if (value.ValueKind == JsonValueKind.String &&
            value.GetString() is { } timestamp &&
            CliDateTimeParsing.TryParseDockerTimestamp(timestamp, out var createdAt))
        {
            return createdAt;
        }

        throw new JsonException("'CreatedAt' の値が不正です。");
    }
}

internal sealed class ContainerListItemDtoJsonConverter : JsonConverter<ContainerListItemDto>
{
    public override bool HandleNull => true;

    public override ContainerListItemDto Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return ContainerListItemDto.Parse(document.RootElement);
    }

    public override void Write(Utf8JsonWriter writer, ContainerListItemDto value, JsonSerializerOptions options)
    {
        throw new NotSupportedException();
    }
}
