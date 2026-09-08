namespace AionLauncher.Core;

/// <summary>
/// Containment for relative paths that arrive from outside the launcher (zip entries today,
/// a remote manifest in v1). Nothing may resolve outside the game root — this is the security
/// surface of the whole project, so the rules are explicit rather than platform-inherited.
/// </summary>
public static class SafePath
{
    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>
    /// Resolves <paramref name="relative"/> under <paramref name="root"/>, or returns false if it
    /// is absolute, escapes the root, or is otherwise not a plain relative path.
    /// </summary>
    public static bool TryResolveInside(string root, string relative, out string fullPath)
    {
        fullPath = string.Empty;

        if (string.IsNullOrWhiteSpace(root) || string.IsNullOrWhiteSpace(relative))
            return false;

        // Drive letters ("C:\x"), device paths and NTFS alternate data streams ("file:stream").
        // Checked by hand because Path.IsPathRooted("C:\\x") is false on Linux.
        if (relative.Contains(':'))
            return false;

        // Rooted ("/etc/passwd", "\Windows") and UNC ("\\host\share").
        if (relative[0] is '/' or '\\')
            return false;

        string[] segments = relative.Split('/', '\\');
        foreach (string segment in segments)
        {
            if (segment.Length == 0)
                return false;
            if (segment is "." or "..")
                return false;
            if (!IsCleanSegment(segment))
                return false;
        }

        string rootFull = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        string candidate = Path.GetFullPath(Path.Combine(rootFull, Path.Combine(segments)));

        if (!candidate.StartsWith(rootFull + Path.DirectorySeparatorChar, PathComparison))
            return false;

        fullPath = candidate;
        return true;
    }

    public static string ResolveInside(string root, string relative)
    {
        if (!TryResolveInside(root, relative, out string fullPath))
            throw new ArgumentException($"Путь «{relative}» выходит за пределы папки игры.", nameof(relative));
        return fullPath;
    }

    /// <summary>Normalizes separators for comparison/display; does not validate.</summary>
    public static string ToRelativeForm(string path) => path.Replace('\\', '/').Trim('/');

    private static bool IsCleanSegment(string segment)
    {
        foreach (char c in segment)
        {
            if (char.IsControl(c))
                return false;
            if (c is '<' or '>' or '"' or '|' or '?' or '*')
                return false;
        }

        // Windows silently strips these, which would make the written path differ from the checked one.
        return segment[^1] is not (' ' or '.');
    }
}
