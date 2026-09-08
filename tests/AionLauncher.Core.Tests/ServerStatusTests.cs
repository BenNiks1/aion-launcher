namespace AionLauncher.Core.Tests;

public class ServerStatusTests
{
    [Fact]
    public void ParsesTheMinimalDocument()
    {
        ServerStatus status = ServerStatus.Parse("""{"online": true, "players": 12}""");

        Assert.True(status.Online);
        Assert.Equal(12, status.Players);
        Assert.Null(status.MaxPlayers);
    }

    [Fact]
    public void ParsesTheFullDocument()
    {
        ServerStatus status = ServerStatus.Parse(
            """{"online": true, "players": 12, "max_players": 100, "message": "сиеж в 20:00", "updated": "2026-09-08T15:21:00Z"}""");

        Assert.Equal(100, status.MaxPlayers);
        Assert.Equal("сиеж в 20:00", status.Message);
        Assert.Equal(new DateTimeOffset(2026, 9, 8, 15, 21, 0, TimeSpan.Zero), status.Updated);
    }

    [Fact]
    public void OfflineDocumentCarriesNoPlayerCount()
    {
        ServerStatus status = ServerStatus.Parse("""{"online": false}""");

        Assert.False(status.Online);
        Assert.Null(status.Players);
        Assert.Equal("Сервер недоступен", status.ToDisplayString());
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{"players": 3}""")]
    [InlineData("""{"online": "yes"}""")]
    public void RejectsAnythingWithoutABooleanOnlineField(string json)
    {
        Assert.Throws<FormatException>(() => ServerStatus.Parse(json));
    }

    [Fact]
    public void IgnoresUnknownAndMistypedOptionalFields()
    {
        ServerStatus status = ServerStatus.Parse(
            """{"online": true, "players": "many", "future_field": {"a": 1}, "max_players": -5}""");

        Assert.True(status.Online);
        Assert.Null(status.Players);
        Assert.Null(status.MaxPlayers);
    }

    [Theory]
    [InlineData(0, "игроков")]
    [InlineData(1, "игрок")]
    [InlineData(2, "игрока")]
    [InlineData(4, "игрока")]
    [InlineData(5, "игроков")]
    [InlineData(11, "игроков")]
    [InlineData(12, "игроков")]
    [InlineData(21, "игрок")]
    [InlineData(22, "игрока")]
    [InlineData(111, "игроков")]
    public void RussianPluralIsCorrect(int count, string expected)
    {
        Assert.Equal(expected, ServerStatus.PluralizePlayers(count));
    }

    [Fact]
    public void DisplayStringReadsNaturally()
    {
        Assert.Equal("Онлайн · 1 игрок", new ServerStatus(true, 1).ToDisplayString());
        Assert.Equal("Онлайн · 12 игроков из 100", new ServerStatus(true, 12, 100).ToDisplayString());
        Assert.Equal("Онлайн", new ServerStatus(true).ToDisplayString());
    }
}
