namespace AionLauncher.Core;

/// <summary>
/// A live client file is never written in place: we stage the full new content and then move it
/// onto the target, so a killed launcher can never leave a truncated <c>version.dll</c> behind.
/// </summary>
public static class AtomicFile
{
    public static void ReplaceWith(string stagedFile, string targetPath)
    {
        string? dir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        File.Move(stagedFile, targetPath, overwrite: true);
    }

    public static void WriteAllBytes(string targetPath, byte[] content)
    {
        string? dir = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        string staged = targetPath + ".tmp-" + Guid.NewGuid().ToString("N")[..8];
        try
        {
            File.WriteAllBytes(staged, content);
            File.Move(staged, targetPath, overwrite: true);
        }
        catch
        {
            TryDelete(staged);
            throw;
        }
    }

    public static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
            // A locked leftover in the staging directory is not worth failing a launch over.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
