using Hasi.Core.Power;

namespace Hasi.Core.Tests;

/// <summary>The first four rows are the programs in the tester's log of 2026-10-23 (O-02), with their real file data.</summary>
public class ProgramNameTests
{
    [Theory]
    [InlineData("Notepad.exe", "Notepad", "Notepad", "Notepad")]
    [InlineData("Notepad.exe", "Microsoft® Windows® Operating System", "notepad", "Notepad")]
    [InlineData("WhatsApp.Root", "WhatsApp.Root", "WhatsApp.Root", "WhatsApp")]
    [InlineData("Windows Terminal Host", "Windows Terminal", "WindowsTerminal", "Windows Terminal")]
    [InlineData("Firefox", "Firefox", "firefox", "Firefox")]
    [InlineData("Microsoft Edge", "Microsoft Edge", "msedge", "Microsoft Edge")]
    [InlineData("Microsoft Word", "Microsoft Office", "WINWORD", "Microsoft Word")]
    [InlineData("Google Chrome", "Google Chrome", "chrome", "Google Chrome")]
    [InlineData("", "", "calc", "calc")]
    [InlineData(null, null, "Some.Tool", "Some")]
    [InlineData("  Total   Commander™  ", null, "TOTALCMD64", "Total Commander")]
    public void A_name_worth_saying_is_chosen(string? description, string? product, string process, string expected)
    {
        Assert.Equal(expected, ProgramName.Choose(description, product, process));
    }
}
