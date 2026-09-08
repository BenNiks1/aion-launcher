using System.Text.RegularExpressions;

namespace AionLauncher.Core.Tests;

public class PatchCheckTests
{
    [Fact]
    public void ReportsMissingBothWhenTheClientIsUnpatched()
    {
        using var game = new TempGameFolder();

        PatchCheckResult result = PatchCheck.Check(game.Root);

        Assert.Equal(PatchProblem.MissingBoth, result.Problem);
        Assert.False(result.IsPatched);
        Assert.True(result.NeedsInstall);
    }

    [Fact]
    public void ReportsTheMissingSideWhenOnlyOneDllIsThere()
    {
        using var game = new TempGameFolder();
        game.WriteFile(PatchCheck.RelativeDll64, "sixty-four");

        Assert.Equal(PatchProblem.Missing32, PatchCheck.Check(game.Root).Problem);

        game.WriteFile(PatchCheck.RelativeDll32, "thirty-two");
        game.Delete(PatchCheck.RelativeDll64);

        Assert.Equal(PatchProblem.Missing64, PatchCheck.Check(game.Root).Problem);
    }

    [Fact]
    public void DetectsTheBin32OverBin64MixUp()
    {
        using var game = new TempGameFolder();
        game.WriteFile(PatchCheck.RelativeDll32, "same bytes");
        game.WriteFile(PatchCheck.RelativeDll64, "same bytes");

        PatchCheckResult result = PatchCheck.Check(game.Root);

        Assert.Equal(PatchProblem.SameDllInBothFolders, result.Problem);
        Assert.False(result.IsPatched);
        Assert.True(result.NeedsInstall);
    }

    [Fact]
    public void TwoDifferentDllsThatAreNotUpstreamAreAcceptedButFlaggedAsUnknown()
    {
        using var game = new TempGameFolder();
        game.WriteFile(PatchCheck.RelativeDll32, "thirty-two");
        game.WriteFile(PatchCheck.RelativeDll64, "sixty-four");

        PatchCheckResult result = PatchCheck.Check(game.Root);

        Assert.Equal(PatchProblem.UnknownBuild, result.Problem);
        Assert.True(result.IsPatched);
        Assert.False(result.NeedsInstall);
        Assert.NotEqual(result.Sha32, result.Sha64);
    }

    [Fact]
    public void ThePinnedUpstreamHashesAreDistinctLowercaseSha256()
    {
        Assert.NotEqual(PatchCheck.Upstream32Sha256, PatchCheck.Upstream64Sha256);
        Assert.Matches(new Regex("^[0-9a-f]{64}$"), PatchCheck.Upstream32Sha256);
        Assert.Matches(new Regex("^[0-9a-f]{64}$"), PatchCheck.Upstream64Sha256);
        Assert.Matches(new Regex("^[0-9a-f]{64}$"), PatchInstaller.DefaultPatchZipSha256);
    }

    [Fact]
    public void Sha256OfFileMatchesTheKnownVectorForAnEmptyFile()
    {
        using var game = new TempGameFolder();
        game.WriteFile("empty.bin", string.Empty);

        Assert.Equal(
            "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            PatchCheck.Sha256OfFile(game.Resolve("empty.bin")));
    }
}
