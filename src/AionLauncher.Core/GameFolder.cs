namespace AionLauncher.Core;

public enum GameFolderProblem
{
    None,
    NotFound,
    MissingClient64,
    MissingClient32,
    MissingData,
    MissingL10N,
}

public sealed record GameFolderCheck(string Path, GameFolderProblem Problem)
{
    public bool IsValid => Problem == GameFolderProblem.None;

    public string ToDisplayString() => Problem switch
    {
        GameFolderProblem.None => "Клиент найден",
        GameFolderProblem.NotFound => "Папка не найдена",
        GameFolderProblem.MissingClient64 => $"Нет «{LaunchArgs.ClientExe64}»",
        GameFolderProblem.MissingClient32 => $"Нет «{LaunchArgs.ClientExe32}»",
        GameFolderProblem.MissingData => "Нет папки «Data»",
        GameFolderProblem.MissingL10N => "Нет папки «L10N»",
        _ => "Папка игры не распознана",
    };
}

/// <summary>
/// Finding the game folder. There is no installer and nothing in the registry — this is a repacked
/// archive, so detection is: what the config says, what the player picked last time, then the
/// launcher's own directory (the recommended "drop the exe into the game root" layout).
/// </summary>
public static class GameFolder
{
    public static GameFolderCheck Validate(string? dir)
    {
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            return new GameFolderCheck(dir ?? string.Empty, GameFolderProblem.NotFound);

        string root = Path.GetFullPath(dir);

        if (!File.Exists(Path.Combine(root, "bin64", "aion.bin")))
            return new GameFolderCheck(root, GameFolderProblem.MissingClient64);
        if (!File.Exists(Path.Combine(root, "bin32", "aion.bin")))
            return new GameFolderCheck(root, GameFolderProblem.MissingClient32);
        if (!Directory.Exists(Path.Combine(root, "Data")))
            return new GameFolderCheck(root, GameFolderProblem.MissingData);
        if (!Directory.Exists(Path.Combine(root, "L10N")))
            return new GameFolderCheck(root, GameFolderProblem.MissingL10N);

        return new GameFolderCheck(root, GameFolderProblem.None);
    }

    /// <summary>First candidate that validates, in priority order. Null when the player must pick one.</summary>
    public static string? Detect(string? configured, string? remembered, string? exeDir)
    {
        foreach (string? candidate in new[] { configured, remembered, exeDir })
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;
            if (Validate(candidate) is { IsValid: true } check)
                return check.Path;
        }

        return null;
    }
}
