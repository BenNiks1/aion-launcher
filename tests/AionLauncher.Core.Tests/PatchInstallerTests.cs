using System.IO.Compression;
using System.Net;
using System.Text;

namespace AionLauncher.Core.Tests;

public class PatchInstallerTests
{
    [Fact]
    public void InstallsBothDllsAndLeavesNoStagingFiles()
    {
        using var game = new TempGameFolder();
        using MemoryStream zip = BuildZip(
            (PatchCheck.RelativeDll32, "thirty-two"),
            (PatchCheck.RelativeDll64, "sixty-four"));

        PatchInstallResult result = PatchInstaller.InstallFromZip(zip, game.Root);

        Assert.Equal(2, result.InstalledFiles.Count);
        Assert.Equal("thirty-two", File.ReadAllText(game.Resolve(PatchCheck.RelativeDll32)));
        Assert.Equal("sixty-four", File.ReadAllText(game.Resolve(PatchCheck.RelativeDll64)));
        Assert.True(PatchCheck.Check(game.Root).IsPatched);
        AssertStagingIsClean(game);
    }

    [Fact]
    public void OverwritesAnExistingDllWithoutLeavingItTruncated()
    {
        using var game = new TempGameFolder();
        game.WriteFile(PatchCheck.RelativeDll64, "old and stale");
        using MemoryStream zip = BuildZip(
            (PatchCheck.RelativeDll32, "thirty-two"),
            (PatchCheck.RelativeDll64, "sixty-four"));

        PatchInstaller.InstallFromZip(zip, game.Root);

        Assert.Equal("sixty-four", File.ReadAllText(game.Resolve(PatchCheck.RelativeDll64)));
    }

    [Theory]
    [InlineData("../evil.dll")]
    [InlineData("..\\..\\Windows\\System32\\evil.dll")]
    [InlineData("bin64/../../evil.dll")]
    public void AZipSlipEntryAbortsTheWholeInstall(string hostileEntry)
    {
        using var game = new TempGameFolder();
        using MemoryStream zip = BuildZip(
            (PatchCheck.RelativeDll32, "thirty-two"),
            (hostileEntry, "payload"),
            (PatchCheck.RelativeDll64, "sixty-four"));

        Assert.Throws<InvalidDataException>(() => PatchInstaller.InstallFromZip(zip, game.Root));
        AssertStagingIsClean(game);
    }

    [Fact]
    public void UnexpectedButHarmlessEntriesAreIgnored()
    {
        using var game = new TempGameFolder();
        using MemoryStream zip = BuildZip(
            (PatchCheck.RelativeDll32, "thirty-two"),
            ("README.md", "hello"),
            (PatchCheck.RelativeDll64, "sixty-four"));

        PatchInstallResult result = PatchInstaller.InstallFromZip(zip, game.Root);

        Assert.Equal(2, result.InstalledFiles.Count);
        Assert.False(File.Exists(game.Resolve("README.md")));
    }

    [Fact]
    public void AnArchiveMissingOneDllIsRejectedAndTheClientIsUntouched()
    {
        using var game = new TempGameFolder();
        game.WriteFile(PatchCheck.RelativeDll64, "still here");
        using MemoryStream zip = BuildZip((PatchCheck.RelativeDll32, "thirty-two"));

        Assert.Throws<InvalidDataException>(() => PatchInstaller.InstallFromZip(zip, game.Root));

        Assert.Equal("still here", File.ReadAllText(game.Resolve(PatchCheck.RelativeDll64)));
        Assert.False(File.Exists(game.Resolve(PatchCheck.RelativeDll32)));
        AssertStagingIsClean(game);
    }

    [Theory]
    [InlineData("bin32/version.dll", true)]
    [InlineData("bin64\\version.dll", true)]
    [InlineData("bin64/other.dll", false)]
    [InlineData("version.dll", false)]
    [InlineData("", false)]
    public void OnlyTheTwoVersionDllsAreOnTheAllowlist(string entry, bool allowed)
    {
        Assert.Equal(allowed, PatchInstaller.IsAllowedEntry(entry));
    }

    [Fact]
    public async Task ADownloadOverPlainHttpIsRefused()
    {
        using var game = new TempGameFolder();
        using var http = new HttpClient(new StubHandler(Array.Empty<byte>()));

        await Assert.ThrowsAsync<ArgumentException>(() => PatchInstaller.DownloadAndInstallAsync(
            "http://example.com/version-dll.zip", null, game.Root, http));
    }

    [Fact]
    public async Task AHashMismatchLeavesTheClientUntouched()
    {
        using var game = new TempGameFolder();
        game.WriteFile(PatchCheck.RelativeDll64, "still here");

        using MemoryStream zip = BuildZip(
            (PatchCheck.RelativeDll32, "thirty-two"),
            (PatchCheck.RelativeDll64, "sixty-four"));
        using var http = new HttpClient(new StubHandler(zip.ToArray()));

        InvalidDataException error = await Assert.ThrowsAsync<InvalidDataException>(
            () => PatchInstaller.DownloadAndInstallAsync(
                "https://example.com/version-dll.zip",
                new string('0', 64),
                game.Root,
                http));

        Assert.Contains("сумма", error.Message, StringComparison.Ordinal);
        Assert.Equal("still here", File.ReadAllText(game.Resolve(PatchCheck.RelativeDll64)));
        Assert.False(File.Exists(game.Resolve(PatchCheck.RelativeDll32)));
        AssertStagingIsClean(game);
    }

    [Fact]
    public async Task AMatchingHashInstallsTheArchive()
    {
        using var game = new TempGameFolder();
        using MemoryStream zip = BuildZip(
            (PatchCheck.RelativeDll32, "thirty-two"),
            (PatchCheck.RelativeDll64, "sixty-four"));

        byte[] payload = zip.ToArray();
        string sha256 = PatchCheck.Sha256OfStream(new MemoryStream(payload));
        using var http = new HttpClient(new StubHandler(payload));

        PatchInstallResult result = await PatchInstaller.DownloadAndInstallAsync(
            "https://example.com/version-dll.zip", sha256, game.Root, http);

        Assert.Equal(2, result.InstalledFiles.Count);
        Assert.Equal("sixty-four", File.ReadAllText(game.Resolve(PatchCheck.RelativeDll64)));
        AssertStagingIsClean(game);
    }

    private static void AssertStagingIsClean(TempGameFolder game)
    {
        string staging = Path.Combine(game.Root, PatchInstaller.StagingDirectoryName, "tmp");
        Assert.True(
            !Directory.Exists(staging) || Directory.GetFileSystemEntries(staging).Length == 0,
            "Staging directory still holds files after the install finished.");
    }

    private static MemoryStream BuildZip(params (string Path, string Content)[] entries)
    {
        var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string path, string content) in entries)
            {
                ZipArchiveEntry entry = archive.CreateEntry(path);
                using Stream stream = entry.Open();
                stream.Write(Encoding.UTF8.GetBytes(content));
            }
        }

        buffer.Position = 0;
        return buffer;
    }

    private sealed class StubHandler(byte[] payload) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(payload),
            });
    }
}
