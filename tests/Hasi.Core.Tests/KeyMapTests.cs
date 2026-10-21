using Hasi.Core.Input;

namespace Hasi.Core.Tests;

public class KeyMapTests
{
    [Theory]
    [InlineData("Down")]
    [InlineData("Ctrl+H")]
    [InlineData("Ctrl+Shift+D")]
    [InlineData("Alt+F4")]
    public void Chords_round_trip_through_text(string text)
    {
        Assert.Equal(text, KeyChord.Parse(text).ToString());
    }

    [Theory]
    [InlineData(Command.Help)]
    [InlineData(Command.RepeatLast)]
    [InlineData(Command.WhereAmI)]
    [InlineData(Command.First)]
    [InlineData(Command.Last)]
    [InlineData(Command.Next)]
    [InlineData(Command.Previous)]
    [InlineData(Command.Activate)]
    [InlineData(Command.Back)]
    [InlineData(Command.StopSpeech)]
    public void Every_everyday_command_works_without_function_keys(Command command)
    {
        var chords = KeyMap.CreateDefault().ChordsFor(command);

        Assert.Contains(chords, c => c.Key is not (>= Key.F1 and <= Key.F12) && c.Key is not (>= Key.NumPad0 and <= Key.Decimal)
                                     && c.Key is not (Key.Home or Key.End));
    }

    [Fact]
    public void Bindings_can_be_changed()
    {
        var map = KeyMap.CreateDefault().Bind(Key.F5, Command.Help);

        Assert.Equal(Command.Help, map.Resolve(Key.F5));
        Assert.True(map.Unbind(Key.F5));
        Assert.Equal(Command.None, map.Resolve(Key.F5));
    }
}
