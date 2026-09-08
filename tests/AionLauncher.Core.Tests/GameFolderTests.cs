namespace AionLauncher.Core.Tests;

public class GameFolderTests
{
    [Fact]
    public void AcceptsAClientWithBin32Bin64DataAndL10N()
    {
        using var game = new TempGameFolder();

        GameFolderCheck check = GameFolder.Validate(game.Root);

        Assert.True(check.IsValid);
        Assert.Equal(GameFolderProblem.None, check.Problem);
    }

    [Fact]
    public void RejectsAFolderWithoutTheSixtyFourBitClient()
    {
        using var game = new TempGameFolder();
        game.Delete("bin64/aion.bin");

        GameFolderCheck check = GameFolder.Validate(game.Root);

        Assert.False(check.IsValid);
        Assert.Equal(GameFolderProblem.MissingClient64, check.Problem);
        Assert.Contains("bin64", check.ToDisplayString(), StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAFolderWithoutData()
    {
        using var game = new TempGameFolder();
        Directory.Delete(game.Resolve("Data"));

        Assert.Equal(GameFolderProblem.MissingData, GameFolder.Validate(game.Root).Problem);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsAnEmptyPath(string? path)
    {
        Assert.Equal(GameFolderProblem.NotFound, GameFolder.Validate(path).Problem);
    }

    [Fact]
    public void DetectPrefersTheConfiguredFolder()
    {
        using var configured = new TempGameFolder();
        using var exeDir = new TempGameFolder();

        Assert.Equal(configured.Root, GameFolder.Detect(configured.Root, null, exeDir.Root));
    }

    [Fact]
    public void DetectFallsBackToTheRememberedFolderThenToTheExeDirectory()
    {
        using var remembered = new TempGameFolder();
        using var exeDir = new TempGameFolder();
        using var broken = new TempGameFolder(complete: false);

        Assert.Equal(remembered.Root, GameFolder.Detect(broken.Root, remembered.Root, exeDir.Root));
        Assert.Equal(exeDir.Root, GameFolder.Detect(null, null, exeDir.Root));
    }

    [Fact]
    public void DetectReturnsNullWhenNothingValidates()
    {
        using var broken = new TempGameFolder(complete: false);

        Assert.Null(GameFolder.Detect(broken.Root, null, broken.Root));
    }
}
