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
    [InlineData(Command.SayTime)]
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
    public void Control_T_only_says_time_and_is_global()
    {
        var map = KeyMap.CreateDefault();
        var chord = new KeyChord(Key.T, KeyModifiers.Ctrl);

        Assert.Equal(Command.SayTime, map.Resolve(chord));
        Assert.Equal(chord, Assert.Single(map.ChordsFor(Command.SayTime)));
        Assert.True(Command.SayTime.IsGlobal());
        Assert.Equal(Command.Help, map.Resolve(new KeyChord(Key.H, KeyModifiers.Ctrl)));
        Assert.Equal(Command.None, map.Resolve(Key.T));
    }

    [Theory]
    [InlineData(Key.PageDown, Command.NextPage)]
    [InlineData(Key.NumPad3, Command.NextPage)]
    [InlineData(Key.PageUp, Command.PreviousPage)]
    [InlineData(Key.NumPad9, Command.PreviousPage)]
    public void Page_commands_have_keypad_equivalents(Key key, Command command)
    {
        Assert.Equal(command, KeyMap.CreateDefault().Resolve(key));
        Assert.False(command.IsGlobal());
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
