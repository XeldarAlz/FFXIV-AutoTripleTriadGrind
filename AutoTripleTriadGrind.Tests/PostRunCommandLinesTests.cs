using AutoTripleTriadGrind.Core.Tasks;

namespace AutoTripleTriadGrind.Tests;

public sealed class PostRunCommandLinesTests
{
    [Fact]
    public void EmptyBlockHasNoLines()
        => Assert.Empty(PostRunCommandLines.Split(string.Empty));

    [Fact]
    public void WhitespaceBlockHasNoLines()
        => Assert.Empty(PostRunCommandLines.Split("  \r\n\t\n "));

    [Fact]
    public void LinesAreTrimmedAndKeepTheirOrder()
        => Assert.Equal(["/li home", "/ays m"], PostRunCommandLines.Split("  /li home \r\n/ays m\r\n"));

    [Fact]
    public void BlankLinesBetweenCommandsAreDropped()
        => Assert.Equal(["/li home", "/ays m"], PostRunCommandLines.Split("/li home\n\n   \n/ays m"));

    [Fact]
    public void LinesWithoutASlashAreKeptForTheCallerToReject()
        => Assert.Equal(["/li home", "hello there"], PostRunCommandLines.Split("/li home\nhello there"));

    [Fact]
    public void SlashLineIsACommand()
        => Assert.True(PostRunCommandLines.IsCommand("/ays m"));

    [Fact]
    public void PlainTextIsNotACommand()
        => Assert.False(PostRunCommandLines.IsCommand("ays m"));

    [Fact]
    public void EmptyLineIsNotACommand()
        => Assert.False(PostRunCommandLines.IsCommand(string.Empty));
}
