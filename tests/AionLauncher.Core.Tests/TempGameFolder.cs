namespace AionLauncher.Core.Tests;

/// <summary>A throwaway directory shaped like a real Aion client.</summary>
public sealed class TempGameFolder : IDisposable
{
    public TempGameFolder(bool complete = true)
    {
        Root = Path.Combine(Path.GetTempPath(), "aion-launcher-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);

        if (!complete)
            return;

        WriteFile("bin64/aion.bin", "client64");
        WriteFile("bin32/aion.bin", "client32");
        Directory.CreateDirectory(Path.Combine(Root, "Data"));
        Directory.CreateDirectory(Path.Combine(Root, "L10N"));
    }

    public string Root { get; }

    public string Resolve(string relative) =>
        Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));

    public void WriteFile(string relative, string content)
    {
        string full = Resolve(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    public void Delete(string relative) => File.Delete(Resolve(relative));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
