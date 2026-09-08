namespace AionLauncher.Core.Tests;

public class LaunchArgsTests
{
    [Theory]
    [InlineData("-ip:play.example.com")]
    [InlineData("-ip:192.168.1.10")]
    [InlineData("-ip:127.0.0.1")]
    [InlineData("-port:2106")]
    [InlineData("-port:1")]
    [InlineData("-port:65535")]
    [InlineData("-loginex")]
    public void AllowsExactlyTheThreeClientFlags(string arg)
    {
        Assert.True(LaunchArgs.IsAllowed(arg));
    }

    [Theory]
    [InlineData("-port:70000")]
    [InlineData("-port:0")]
    [InlineData("-port:-1")]
    [InlineData("-port:abc")]
    [InlineData("-ip:a b")]
    [InlineData("-ip:play.example.com && calc.exe")]
    [InlineData("&& calc.exe")]
    [InlineData("-foo")]
    [InlineData("-loginex extra")]
    [InlineData("-ip:")]
    [InlineData("-ip:::1")]
    [InlineData("")]
    [InlineData(null)]
    public void RejectsEverythingElse(string? arg)
    {
        Assert.False(LaunchArgs.IsAllowed(arg));
    }

    [Fact]
    public void BuildProducesTheClientCommandLine()
    {
        IReadOnlyList<string> args = LaunchArgs.Build("play.example.com", 2106);

        Assert.Equal(["-ip:play.example.com", "-port:2106", "-loginex"], args);
        Assert.Equal("-ip:play.example.com -port:2106 -loginex", LaunchArgs.ToCommandLine(args));
    }

    [Theory]
    [InlineData("play example.com", 2106)]
    [InlineData("play.example.com", 70000)]
    [InlineData("play.example.com", 0)]
    public void BuildRejectsBadInput(string host, int port)
    {
        Assert.Throws<ArgumentException>(() => LaunchArgs.Build(host, port));
    }

    [Fact]
    public void ValidateNamesTheOffendingArgument()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(
            () => LaunchArgs.Validate(["-ip:play.example.com", "-dev_mode"]));

        Assert.Contains("-dev_mode", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("bin64/aion.bin")]
    [InlineData("bin32/aion.bin")]
    [InlineData("bin64\\aion.bin")]
    [InlineData("BIN64/AION.BIN")]
    public void OnlyTheTwoRealClientExecutablesAreAllowed(string relative)
    {
        Assert.True(LaunchArgs.IsAllowedClientExe(relative));
    }

    [Theory]
    [InlineData("bin64/evil.exe")]
    [InlineData("../aion.bin")]
    [InlineData("C:\\Windows\\System32\\cmd.exe")]
    [InlineData("aion.bin")]
    [InlineData("")]
    [InlineData(null)]
    public void AnyOtherExecutableIsRejected(string? relative)
    {
        Assert.False(LaunchArgs.IsAllowedClientExe(relative));
    }

    [Fact]
    public void ResolveClientExePrefers64BitAndFallsBackTo32()
    {
        using var game = new TempGameFolder();

        Assert.Equal(game.Resolve("bin64/aion.bin"), LaunchArgs.ResolveClientExe(game.Root));
        Assert.Equal(game.Resolve("bin32/aion.bin"), LaunchArgs.ResolveClientExe(game.Root, prefer32Bit: true));

        game.Delete("bin64/aion.bin");
        Assert.Equal(game.Resolve("bin32/aion.bin"), LaunchArgs.ResolveClientExe(game.Root));
    }

    [Fact]
    public void ResolveClientExeThrowsWhenNeitherExists()
    {
        using var game = new TempGameFolder(complete: false);

        Assert.Throws<FileNotFoundException>(() => LaunchArgs.ResolveClientExe(game.Root));
    }
}
