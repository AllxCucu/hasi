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

    [Theory]
    [InlineData(Key.Left, KeyModifiers.None, Command.PreviousCharacter)]
    [InlineData(Key.Right, KeyModifiers.None, Command.NextCharacter)]
    [InlineData(Key.Left, KeyModifiers.Ctrl, Command.PreviousWord)]
    [InlineData(Key.Right, KeyModifiers.Ctrl, Command.NextWord)]
    [InlineData(Key.Home, KeyModifiers.None, Command.LineStart)]
    [InlineData(Key.End, KeyModifiers.None, Command.LineEnd)]
    [InlineData(Key.Home, KeyModifiers.Ctrl, Command.DocumentStart)]
    [InlineData(Key.Up, KeyModifiers.Ctrl, Command.DocumentStart)]
    [InlineData(Key.End, KeyModifiers.Ctrl, Command.DocumentEnd)]
    [InlineData(Key.Down, KeyModifiers.Ctrl, Command.DocumentEnd)]
    [InlineData(Key.Enter, KeyModifiers.None, Command.NewLine)]
    [InlineData(Key.Delete, KeyModifiers.None, Command.DeleteNext)]
    [InlineData(Key.S, KeyModifiers.Ctrl, Command.Save)]
    [InlineData(Key.Z, KeyModifiers.Ctrl, Command.Undo)]
    [InlineData(Key.Y, KeyModifiers.Ctrl, Command.Redo)]
    [InlineData(Key.F10, KeyModifiers.None, Command.ScreenMenu)]
    [InlineData(Key.M, KeyModifiers.Ctrl, Command.ScreenMenu)]
    public void The_text_editing_profile_overrides_the_default_keys(Key key, KeyModifiers modifiers, Command command)
    {
        var map = KeyMap.CreateDefault();
        var chord = new KeyChord(key, modifiers);

        Assert.Equal(command, map.Resolve(chord, KeyProfile.TextEditing));
        Assert.NotEqual(command, map.Resolve(chord, KeyProfile.Default));
        Assert.NotEqual(command, map.Resolve(chord));
    }

    [Theory]
    [InlineData(Key.Up, Command.Previous)]
    [InlineData(Key.Down, Command.Next)]
    [InlineData(Key.Backspace, Command.DeletePrevious)]
    [InlineData(Key.Escape, Command.Back)]
    [InlineData(Key.PageDown, Command.NextPage)]
    public void The_text_editing_profile_falls_back_to_the_default_keys(Key key, Command command)
    {
        Assert.Equal(command, KeyMap.CreateDefault().Resolve(key, KeyProfile.TextEditing));
    }

    [Fact]
    public void Every_global_key_keeps_its_command_in_the_text_editing_profile()
    {
        var map = KeyMap.CreateDefault();
        var globals = map.Bindings.Where(b => b.Value.IsGlobal()).ToList();

        Assert.Contains(globals, b => b.Key == new KeyChord(Key.H, KeyModifiers.Ctrl));
        Assert.Contains(globals, b => b.Key == new KeyChord(Key.R, KeyModifiers.Ctrl));
        Assert.Contains(globals, b => b.Key == new KeyChord(Key.U, KeyModifiers.Ctrl));
        Assert.Contains(globals, b => b.Key == new KeyChord(Key.T, KeyModifiers.Ctrl));
        foreach (var (chord, command) in globals)
        {
            Assert.Equal(command, map.Resolve(chord, KeyProfile.TextEditing));
        }
        Assert.DoesNotContain(map.ProfileBindings(KeyProfile.TextEditing), b => b.Value.IsGlobal() || map.Resolve(b.Key).IsGlobal());
    }

    [Fact]
    public void The_default_profile_is_unchanged_by_the_editing_layer()
    {
        var map = KeyMap.CreateDefault();

        Assert.Equal(Command.First, map.Resolve(Key.Home, KeyProfile.Default));
        Assert.Equal(Command.Last, map.Resolve(new KeyChord(Key.Down, KeyModifiers.Ctrl), KeyProfile.Default));
        Assert.Equal(Command.Activate, map.Resolve(Key.Enter, KeyProfile.Default));
        Assert.Equal(Command.Decrease, map.Resolve(Key.Left, KeyProfile.Default));
        Assert.Equal(Command.None, map.Resolve(Key.Delete, KeyProfile.Default));
        Assert.Equal(Command.None, map.Resolve(new KeyChord(Key.S, KeyModifiers.Ctrl), KeyProfile.Default));
    }

    [Fact]
    public void Profile_bindings_can_be_changed_and_removed()
    {
        var map = KeyMap.CreateDefault().Bind(Key.F5, Command.Save, KeyProfile.TextEditing);

        Assert.Equal(Command.Save, map.Resolve(Key.F5, KeyProfile.TextEditing));
        Assert.Equal(Command.None, map.Resolve(Key.F5));

        Assert.True(map.Unbind(Key.Left, KeyProfile.TextEditing));
        Assert.Equal(Command.Decrease, map.Resolve(Key.Left, KeyProfile.TextEditing)); // falls back to the default
        Assert.False(map.Unbind(Key.Left, KeyProfile.TextEditing));
    }

    [Theory]
    [InlineData(Command.PreviousCharacter)]
    [InlineData(Command.NextCharacter)]
    [InlineData(Command.PreviousWord)]
    [InlineData(Command.NextWord)]
    [InlineData(Command.DeleteNext)]
    public void Moving_and_deleting_in_text_repeat_when_held(Command command)
    {
        Assert.True(command.RepeatsWhenHeld());
    }

    [Theory]
    [InlineData(Command.NewLine)]
    [InlineData(Command.Undo)]
    [InlineData(Command.Redo)]
    [InlineData(Command.Save)]
    [InlineData(Command.ScreenMenu)]
    public void Editing_commands_that_change_much_do_not_repeat_when_held(Command command)
    {
        Assert.False(command.RepeatsWhenHeld());
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
