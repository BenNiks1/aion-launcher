using System.Security.Cryptography;

namespace AionLauncher.Core;

public enum PatchProblem
{
    None,
    MissingBoth,
    Missing32,
    Missing64,
    SameDllInBothFolders,
    UnknownBuild,
}

public sealed record PatchCheckResult(PatchProblem Problem, string? Sha32, string? Sha64)
{
    public bool IsPatched => Problem is PatchProblem.None or PatchProblem.UnknownBuild;

    /// <summary>True when downloading the upstream patch would fix the situation.</summary>
    public bool NeedsInstall => Problem is PatchProblem.MissingBoth or PatchProblem.Missing32
        or PatchProblem.Missing64 or PatchProblem.SameDllInBothFolders;

    public string ToDisplayString() => Problem switch
    {
        PatchProblem.None => $"Патч клиента установлен ({PatchCheck.UpstreamVersion})",
        PatchProblem.UnknownBuild => "Патч клиента установлен (сборка не опознана)",
        PatchProblem.MissingBoth => "Клиент не пропатчен: нет version.dll в bin32 и bin64",
        PatchProblem.Missing32 => "Клиент не пропатчен: нет bin32\\version.dll",
        PatchProblem.Missing64 => "Клиент не пропатчен: нет bin64\\version.dll",
        PatchProblem.SameDllInBothFolders => "В bin32 и bin64 лежит один и тот же version.dll — это разные файлы",
        _ => "Состояние патча неизвестно",
    };
}

/// <summary>
/// Checks that the client carries the upstream <c>version.dll</c> patch. The two DLLs are different
/// binaries; copying one over the other is the most common install mistake, so it gets its own state.
/// </summary>
public static class PatchCheck
{
    public const string UpstreamVersion = "v1.5.0";
    public const string Upstream32Sha256 = "e3227098408f6d36a733860279377941537537d0199f902514866ae745f8a94c";
    public const string Upstream64Sha256 = "550bcec6f2e90ab8c7efc89db3da2d657560bd0c070632c2cc84ed02ec448480";

    public const string RelativeDll32 = "bin32/version.dll";
    public const string RelativeDll64 = "bin64/version.dll";

    public static PatchCheckResult Check(string gameRoot)
    {
        string dll32 = SafePath.ResolveInside(gameRoot, RelativeDll32);
        string dll64 = SafePath.ResolveInside(gameRoot, RelativeDll64);

        bool has32 = File.Exists(dll32);
        bool has64 = File.Exists(dll64);

        if (!has32 && !has64)
            return new PatchCheckResult(PatchProblem.MissingBoth, null, null);
        if (!has32)
            return new PatchCheckResult(PatchProblem.Missing32, null, Sha256OfFile(dll64));
        if (!has64)
            return new PatchCheckResult(PatchProblem.Missing64, Sha256OfFile(dll32), null);

        string sha32 = Sha256OfFile(dll32);
        string sha64 = Sha256OfFile(dll64);

        if (string.Equals(sha32, sha64, StringComparison.OrdinalIgnoreCase))
            return new PatchCheckResult(PatchProblem.SameDllInBothFolders, sha32, sha64);

        bool known = string.Equals(sha32, Upstream32Sha256, StringComparison.OrdinalIgnoreCase)
            && string.Equals(sha64, Upstream64Sha256, StringComparison.OrdinalIgnoreCase);

        return new PatchCheckResult(known ? PatchProblem.None : PatchProblem.UnknownBuild, sha32, sha64);
    }

    public static string Sha256OfFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public static string Sha256OfStream(Stream stream) =>
        Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
}
