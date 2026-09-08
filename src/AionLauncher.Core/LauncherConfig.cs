using System.Globalization;
using System.Text;
using System.Text.Json;

namespace AionLauncher.Core;

/// <summary>
/// <c>launcher.json</c>, shipped next to the exe and editable by the owner without a rebuild.
/// Parsed by hand with <see cref="JsonDocument"/> — no reflection, so single-file publishing and any
/// later trimming stay predictable, and unknown keys are simply ignored.
/// </summary>
public sealed class LauncherConfig
{
    public const string FileName = "launcher.json";

    public string? GameDir { get; set; }

    public string ServerHost { get; set; } = "127.0.0.1";

    public int LoginPort { get; set; } = LaunchArgs.DefaultLoginPort;

    public string? NewsUrl { get; set; }

    public string? StatusUrl { get; set; }

    public bool Prefer32Bit { get; set; }

    public string PatchZipUrl { get; set; } = PatchInstaller.DefaultPatchZipUrl;

    public string? PatchZipSha256 { get; set; } = PatchInstaller.DefaultPatchZipSha256;

    /// <summary>Missing file is not an error: the built-in defaults must always let Play work.</summary>
    public static LauncherConfig LoadOrDefault(string path) =>
        File.Exists(path) ? Parse(File.ReadAllText(path, Encoding.UTF8)) : new LauncherConfig();

    public static LauncherConfig Parse(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });
        }
        catch (JsonException e)
        {
            throw new FormatException($"{FileName} не является корректным JSON: {e.Message}", e);
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new FormatException($"{FileName} должен быть объектом JSON.");

            var config = new LauncherConfig();

            if (ReadString(root, "gameDir") is { } gameDir)
                config.GameDir = gameDir;
            if (ReadString(root, "serverHost") is { } host)
                config.ServerHost = host;
            if (ReadInt(root, "loginPort") is { } port)
                config.LoginPort = port;
            if (ReadString(root, "newsUrl") is { } news)
                config.NewsUrl = news;
            if (ReadString(root, "statusUrl") is { } status)
                config.StatusUrl = status;
            if (ReadBool(root, "prefer32Bit") is { } prefer32)
                config.Prefer32Bit = prefer32;
            if (ReadString(root, "patchZipUrl") is { } patchUrl)
                config.PatchZipUrl = patchUrl;
            if (root.TryGetProperty("patchZipSha256", out JsonElement sha))
                config.PatchZipSha256 = sha.ValueKind == JsonValueKind.String ? sha.GetString() : null;

            config.Validate();
            return config;
        }
    }

    /// <summary>Rejects a config that would produce a bad command line or open a non-web URL.</summary>
    public void Validate()
    {
        if (!LaunchArgs.IsValidHost(ServerHost))
            throw new FormatException($"«serverHost» некорректен: «{ServerHost}».");
        if (!LaunchArgs.IsValidPort(LoginPort))
            throw new FormatException($"«loginPort» вне диапазона 1–65535: {LoginPort}.");
        if (!IsSafeWebUrl(NewsUrl))
            throw new FormatException($"«newsUrl» должен быть http(s)-адресом: «{NewsUrl}».");
        if (!IsSafeWebUrl(StatusUrl))
            throw new FormatException($"«statusUrl» должен быть http(s)-адресом: «{StatusUrl}».");
        if (!string.IsNullOrWhiteSpace(PatchZipUrl)
            && (!Uri.TryCreate(PatchZipUrl, UriKind.Absolute, out Uri? patch) || patch.Scheme != Uri.UriSchemeHttps))
        {
            throw new FormatException($"«patchZipUrl» должен быть https-адресом: «{PatchZipUrl}».");
        }
    }

    /// <summary>Empty is fine (the feature is simply off); anything but http/https is not.</summary>
    public static bool IsSafeWebUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return true;

        return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }

    public IReadOnlyList<string> BuildClientArgs() => LaunchArgs.Build(ServerHost, LoginPort);

    public string ToJson()
    {
        var options = new JsonWriterOptions { Indented = true };
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, options))
        {
            writer.WriteStartObject();
            writer.WriteString("gameDir", GameDir ?? string.Empty);
            writer.WriteString("serverHost", ServerHost);
            writer.WriteNumber("loginPort", LoginPort);
            writer.WriteString("newsUrl", NewsUrl ?? string.Empty);
            writer.WriteString("statusUrl", StatusUrl ?? string.Empty);
            writer.WriteBoolean("prefer32Bit", Prefer32Bit);
            writer.WriteString("patchZipUrl", PatchZipUrl);
            writer.WriteString("patchZipSha256", PatchZipSha256 ?? string.Empty);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    public void Save(string path)
    {
        Validate();
        string? dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        AtomicFile.WriteAllBytes(path, Encoding.UTF8.GetBytes(ToJson()));
    }

    private static string? ReadString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.String)
            return null;
        string? text = value.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static int? ReadInt(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out JsonElement value))
            return null;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number))
            return number;

        // A quoted port is a common hand-edit; accept it rather than failing the whole file.
        if (value.ValueKind == JsonValueKind.String
            && int.TryParse(value.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out int parsed))
        {
            return parsed;
        }

        throw new FormatException($"«{name}» должен быть числом.");
    }

    private static bool? ReadBool(JsonElement root, string name) =>
        root.TryGetProperty(name, out JsonElement value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;
}
