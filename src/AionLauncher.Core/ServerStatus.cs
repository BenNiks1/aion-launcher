using System.Text.Json;

namespace AionLauncher.Core;

/// <summary>
/// The status document published next to the launcher's other static files:
/// <c>{ "online": true, "players": 12, "max_players": 100, "message": null, "updated": "..." }</c>.
/// Only <c>online</c> is required; everything else is optional so the publisher can grow.
/// </summary>
public sealed record ServerStatus(
    bool Online,
    int? Players = null,
    int? MaxPlayers = null,
    string? Message = null,
    DateTimeOffset? Updated = null)
{
    public static ServerStatus Unknown { get; } = new(false, Message: "статус недоступен");

    public static ServerStatus Parse(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException e)
        {
            throw new FormatException("status.json не является корректным JSON.", e);
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new FormatException("status.json должен быть объектом JSON.");

            if (!root.TryGetProperty("online", out JsonElement online)
                || online.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw new FormatException("В status.json нет булева поля «online».");
            }

            return new ServerStatus(
                online.GetBoolean(),
                ReadInt(root, "players"),
                ReadInt(root, "max_players"),
                ReadString(root, "message"),
                ReadDate(root, "updated"));
        }
    }

    public string ToDisplayString()
    {
        if (!Online)
            return string.IsNullOrWhiteSpace(Message) ? "Сервер недоступен" : $"Сервер недоступен — {Message}";

        string text = Players is { } players
            ? $"Онлайн · {players} {PluralizePlayers(players)}"
            : "Онлайн";

        if (MaxPlayers is { } max && Players is not null)
            text += $" из {max}";

        return string.IsNullOrWhiteSpace(Message) ? text : $"{text} · {Message}";
    }

    public static string PluralizePlayers(int count)
    {
        int mod100 = Math.Abs(count) % 100;
        if (mod100 is >= 11 and <= 14)
            return "игроков";

        return (mod100 % 10) switch
        {
            1 => "игрок",
            2 or 3 or 4 => "игрока",
            _ => "игроков",
        };
    }

    private static int? ReadInt(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out int parsed) && parsed >= 0
            ? parsed
            : null;

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static DateTimeOffset? ReadDate(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(value.GetString(), out DateTimeOffset parsed)
            ? parsed
            : null;
}
