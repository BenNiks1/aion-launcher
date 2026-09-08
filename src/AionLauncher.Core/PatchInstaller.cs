using System.IO.Compression;

namespace AionLauncher.Core;

public sealed record PatchInstallResult(IReadOnlyList<string> InstalledFiles);

/// <summary>
/// Installs upstream's <c>version-dll.zip</c> into the game root. The zip holds exactly
/// <c>bin32/version.dll</c> and <c>bin64/version.dll</c> — two different binaries.
/// Nothing outside that allowlist is ever written, the archive hash is pinned, and each file is
/// staged then moved, so an interrupted install cannot corrupt a client.
/// </summary>
public static class PatchInstaller
{
    public const string DefaultPatchZipUrl =
        "https://github.com/beyond-aion/aion-version-dll/releases/download/v1.5.0/version-dll.zip";

    /// <summary>SHA256 of the upstream v1.5.0 archive. Pinned so a swapped release cannot install silently.</summary>
    public const string DefaultPatchZipSha256 = "3c0034fa381a248a73b3034f47fdac40dc8b8665079b1a067920b0c519014670";

    public const string StagingDirectoryName = ".launcher";

    private const long MaxEntryBytes = 8L * 1024 * 1024;

    public static IReadOnlyList<string> AllowedEntries { get; } = [PatchCheck.RelativeDll32, PatchCheck.RelativeDll64];

    public static bool IsAllowedEntry(string? entryFullName)
    {
        if (string.IsNullOrWhiteSpace(entryFullName))
            return false;

        string normalized = SafePath.ToRelativeForm(entryFullName);
        return AllowedEntries.Any(allowed => normalized.Equals(allowed, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Extracts the allowlisted entries of an already-verified archive into the game root.
    /// Entries that cannot be contained inside the root are treated as an attack, not as clutter.
    /// </summary>
    public static PatchInstallResult InstallFromZip(Stream zipStream, string gameRoot)
    {
        if (!Directory.Exists(gameRoot))
            throw new DirectoryNotFoundException($"Папка игры не найдена: {gameRoot}");

        string staging = Path.Combine(Path.GetFullPath(gameRoot), StagingDirectoryName, "tmp");
        Directory.CreateDirectory(staging);

        List<string> installed = [];
        List<(string Staged, string Target)> ready = [];

        try
        {
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                    continue; // directory entry

                if (!SafePath.TryResolveInside(gameRoot, entry.FullName, out string target))
                    throw new InvalidDataException($"Архив содержит небезопасный путь: «{entry.FullName}».");

                if (!IsAllowedEntry(entry.FullName))
                    continue;

                if (entry.Length > MaxEntryBytes)
                    throw new InvalidDataException($"Файл «{entry.FullName}» подозрительно большой ({entry.Length} байт).");

                string staged = Path.Combine(staging, Guid.NewGuid().ToString("N")[..12] + ".part");
                using (Stream source = entry.Open())
                using (FileStream destination = File.Create(staged))
                {
                    source.CopyTo(destination);
                }

                ready.Add((staged, target));
                installed.Add(SafePath.ToRelativeForm(entry.FullName));
            }

            if (installed.Count != AllowedEntries.Count)
            {
                throw new InvalidDataException(
                    $"В архиве нет всех нужных файлов ({string.Join(", ", AllowedEntries)}).");
            }

            // Only once every file is staged do we touch the live client.
            foreach ((string stagedFile, string targetPath) in ready)
                AtomicFile.ReplaceWith(stagedFile, targetPath);

            ready.Clear();
            return new PatchInstallResult(installed);
        }
        finally
        {
            foreach ((string stagedFile, _) in ready)
                AtomicFile.TryDelete(stagedFile);
            TryDeleteDirectory(staging);
        }
    }

    /// <summary>Downloads over HTTPS, verifies the pinned hash, and only then installs.</summary>
    public static async Task<PatchInstallResult> DownloadAndInstallAsync(
        string url,
        string? expectedSha256,
        string gameRoot,
        HttpClient http,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException($"Адрес патча должен быть https://, получено «{url}».", nameof(url));

        string staging = Path.Combine(Path.GetFullPath(gameRoot), StagingDirectoryName, "tmp");
        Directory.CreateDirectory(staging);
        string download = Path.Combine(staging, "version-dll.zip.part");

        try
        {
            using (HttpResponseMessage response =
                await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                await using FileStream file = File.Create(download);
                await response.Content.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
            }

            if (!string.IsNullOrWhiteSpace(expectedSha256))
            {
                string actual;
                await using (FileStream verify = File.OpenRead(download))
                    actual = PatchCheck.Sha256OfStream(verify);

                if (!actual.Equals(expectedSha256.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        $"Контрольная сумма архива не совпала.\nОжидалось: {expectedSha256}\nПолучено:  {actual}");
                }
            }

            await using FileStream verified = File.OpenRead(download);
            return InstallFromZip(verified, gameRoot);
        }
        finally
        {
            AtomicFile.TryDelete(download);
            TryDeleteDirectory(staging);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path) && Directory.GetFileSystemEntries(path).Length == 0)
                Directory.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
