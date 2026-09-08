namespace AionLauncher.Core.Tests;

public class PathSafetyTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "aion-root");

    [Theory]
    [InlineData("../../Windows/System32/x.dll")]
    [InlineData("..\\..\\Windows\\System32\\x.dll")]
    [InlineData("bin64/../../x.dll")]
    [InlineData("C:\\x.dll")]
    [InlineData("/etc/passwd")]
    [InlineData("\\Windows\\x.dll")]
    [InlineData("\\\\host\\share\\x.dll")]
    [InlineData("bin64/version.dll:stream")]
    [InlineData("bin64//version.dll")]
    [InlineData("./bin64/version.dll")]
    [InlineData("bin64/")]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsAnythingThatEscapesOrIsNotRelative(string relative)
    {
        Assert.False(SafePath.TryResolveInside(Root, relative, out _));
    }

    [Theory]
    [InlineData("bin64/version.dll")]
    [InlineData("bin32\\version.dll")]
    [InlineData("Data/patch.pak")]
    [InlineData("L10N/enu/ui.pak")]
    public void AcceptsPlainRelativePathsAndResolvesUnderTheRoot(string relative)
    {
        Assert.True(SafePath.TryResolveInside(Root, relative, out string full));

        string expectedPrefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Root)) + Path.DirectorySeparatorChar;
        Assert.StartsWith(expectedPrefix, full, StringComparison.Ordinal);
        Assert.DoesNotContain("..", full, StringComparison.Ordinal);

        string expectedTail = relative.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        Assert.EndsWith(expectedTail, full, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveInsideThrowsInsteadOfReturningAnEscapingPath()
    {
        Assert.Throws<ArgumentException>(() => SafePath.ResolveInside(Root, "../evil.dll"));
    }
}
