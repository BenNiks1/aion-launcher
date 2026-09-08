namespace AionLauncher.Core.Tests;

public class LauncherConfigTests
{
    [Fact]
    public void ParsesTheShippedShape()
    {
        LauncherConfig config = LauncherConfig.Parse(
            """
            {
              "gameDir": "D:\\Games\\Aion 4.8",
              "serverHost": "play.example.com",
              "loginPort": 2106,
              "newsUrl": "https://example.com/news",
              "statusUrl": "https://example.com/status.json"
            }
            """);

        Assert.Equal("D:\\Games\\Aion 4.8", config.GameDir);
        Assert.Equal("play.example.com", config.ServerHost);
        Assert.Equal(2106, config.LoginPort);
        Assert.Equal(["-ip:play.example.com", "-port:2106", "-loginex"], config.BuildClientArgs());
    }

    [Fact]
    public void MissingKeysFallBackToDefaultsSoPlayAlwaysWorks()
    {
        LauncherConfig config = LauncherConfig.Parse("{}");

        Assert.Equal("127.0.0.1", config.ServerHost);
        Assert.Equal(LaunchArgs.DefaultLoginPort, config.LoginPort);
        Assert.Equal(PatchInstaller.DefaultPatchZipUrl, config.PatchZipUrl);
        Assert.Equal(PatchInstaller.DefaultPatchZipSha256, config.PatchZipSha256);
        Assert.False(config.Prefer32Bit);
    }

    [Fact]
    public void AMissingFileIsNotAnError()
    {
        LauncherConfig config = LauncherConfig.LoadOrDefault(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), LauncherConfig.FileName));

        Assert.Equal("127.0.0.1", config.ServerHost);
    }

    [Fact]
    public void UnknownKeysAreIgnoredSoTheConfigCanGrow()
    {
        LauncherConfig config = LauncherConfig.Parse("""{"serverHost": "play.example.com", "somethingNew": 42}""");

        Assert.Equal("play.example.com", config.ServerHost);
    }

    [Fact]
    public void AQuotedPortIsAcceptedBecauseItIsACommonHandEdit()
    {
        Assert.Equal(2107, LauncherConfig.Parse("""{"loginPort": "2107"}""").LoginPort);
    }

    [Theory]
    [InlineData("""{"serverHost": "play example.com"}""")]
    [InlineData("""{"loginPort": 70000}""")]
    [InlineData("""{"loginPort": 0}""")]
    [InlineData("""{"loginPort": true}""")]
    [InlineData("""{"newsUrl": "javascript:alert(1)"}""")]
    [InlineData("""{"newsUrl": "file:///C:/Windows/System32/calc.exe"}""")]
    [InlineData("""{"statusUrl": "ftp://example.com/status"}""")]
    [InlineData("""{"patchZipUrl": "http://example.com/version-dll.zip"}""")]
    [InlineData("not json")]
    [InlineData("[]")]
    public void ADangerousOrBrokenConfigIsRejected(string json)
    {
        Assert.Throws<FormatException>(() => LauncherConfig.Parse(json));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("https://example.com/news", true)]
    [InlineData("http://192.168.1.10/news", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("file:///etc/passwd", false)]
    [InlineData("example.com/news", false)]
    public void OnlyHttpAndHttpsUrlsMayBeOpened(string? url, bool safe)
    {
        Assert.Equal(safe, LauncherConfig.IsSafeWebUrl(url));
    }

    [Fact]
    public void RoundTripsThroughJson()
    {
        var original = new LauncherConfig
        {
            GameDir = "D:\\Games\\Aion",
            ServerHost = "play.example.com",
            LoginPort = 2107,
            NewsUrl = "https://example.com/news",
            StatusUrl = "https://example.com/status.json",
            Prefer32Bit = true,
        };

        LauncherConfig restored = LauncherConfig.Parse(original.ToJson());

        Assert.Equal(original.GameDir, restored.GameDir);
        Assert.Equal(original.ServerHost, restored.ServerHost);
        Assert.Equal(original.LoginPort, restored.LoginPort);
        Assert.Equal(original.NewsUrl, restored.NewsUrl);
        Assert.Equal(original.StatusUrl, restored.StatusUrl);
        Assert.True(restored.Prefer32Bit);
    }

    [Fact]
    public void UserSettingsRememberThePickedFolderAndSurviveCorruption()
    {
        string dir = Path.Combine(Path.GetTempPath(), "aion-launcher-tests", Guid.NewGuid().ToString("N"));
        string path = UserSettings.DefaultPath(dir);
        try
        {
            new UserSettings { GameDir = "D:\\Games\\Aion" }.Save(path);
            Assert.Equal("D:\\Games\\Aion", UserSettings.LoadOrDefault(path).GameDir);

            File.WriteAllText(path, "{ broken");
            Assert.Null(UserSettings.LoadOrDefault(path).GameDir);
        }
        finally
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
    }
}
