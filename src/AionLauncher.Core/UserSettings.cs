using System.Text;
using System.Text.Json;

namespace AionLauncher.Core;

/// <summary>
/// The one thing the launcher remembers per player: the folder they picked. Kept out of
/// <c>launcher.json</c> so re-downloading the exe (and its shipped config) never loses it, and so a
/// game root the player cannot write to is not a problem.
/// </summary>
public sealed class UserSettings
{
    public const string FileName = "settings.json";

    public string? GameDir { get; set; }

    public static string DefaultPath(string localAppDataDir) =>
        Path.Combine(localAppDataDir, "AionLauncher", FileName);

    public static UserSettings LoadOrDefault(string path)
    {
        if (!File.Exists(path))
            return new UserSettings();

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return new UserSettings();

            return new UserSettings
            {
                GameDir = document.RootElement.TryGetProperty("gameDir", out JsonElement dir)
                    && dir.ValueKind == JsonValueKind.String
                        ? dir.GetString()
                        : null,
            };
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            // A corrupt or unreadable remembered path must never block the launcher.
            return new UserSettings();
        }
    }

    public void Save(string path)
    {
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("gameDir", GameDir ?? string.Empty);
            writer.WriteEndObject();
        }

        AtomicFile.WriteAllBytes(path, buffer.ToArray());
    }
}
