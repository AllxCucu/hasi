using Hasi.Core.Input;
using Hasi.Core.Screens;
using Hasi.Testing;

namespace Hasi.Core.Tests;

public class NumericKeypadTests
{
    private static readonly KeyMap Map = KeyMap.CreateDefault();

    [Theory]
    [InlineData(Key.NumPad8)]
    [InlineData(Key.NumPad0)]
    [InlineData(Key.NumPad5)]
    [InlineData(Key.NumPad3)]
    [InlineData(Key.NumPad9)]
    [InlineData(Key.Decimal)]
    public void In_a_text_field_with_num_lock_on_the_keypad_types(Key key)
    {
        Assert.True(TextRouting.ProducesText(key, Map, textEntryActive: true, numLockOn: true));
    }

    [Theory]
    [InlineData(Key.NumPad8, true, false)]  // text field, Num Lock off (Windows then sends Up anyway)
    [InlineData(Key.NumPad8, false, true)]  // menu, Num Lock on: approved KeyMap, navigation
    [InlineData(Key.NumPad5, false, true)]
    public void Elsewhere_the_keypad_keeps_its_commands(Key key, bool textEntry, bool numLock)
    {
        Assert.False(TextRouting.ProducesText(key, Map, textEntry, numLock));
    }

    [Theory]
    [InlineData(Key.Divide)]
    [InlineData(Key.Multiply)]
    [InlineData(Key.Subtract)]
    public void Keypad_operators_stay_global_commands_even_in_text_fields(Key key)
    {
        Assert.False(TextRouting.ProducesText(key, Map, textEntryActive: true, numLockOn: true));
    }

    [Theory]
    [InlineData(Key.A, KeyModifiers.None, true)]
    [InlineData(Key.A, KeyModifiers.Shift, true)]
    [InlineData(Key.S, KeyModifiers.Ctrl | KeyModifiers.Alt, true)]   // AltGr+S = ș on some layouts
    [InlineData(Key.Other, KeyModifiers.None, true)]                  // ș ț ă î â on the Romanian layout
    [InlineData(Key.T, KeyModifiers.Ctrl, false)]
    [InlineData(Key.H, KeyModifiers.Ctrl, false)]                     // Ctrl+H is help
    [InlineData(Key.Enter, KeyModifiers.None, false)]
    [InlineData(Key.F1, KeyModifiers.None, false)]
    public void Letters_and_punctuation_type_but_commands_do_not(Key key, KeyModifiers modifiers, bool types)
    {
        Assert.Equal(types, TextRouting.ProducesText(new KeyChord(key, modifiers), Map, textEntryActive: true, numLockOn: true));
    }

    [Theory]
    [InlineData(Key.NumPad4)]
    [InlineData(Key.NumPad6)]
    [InlineData(Key.NumPad7)]
    [InlineData(Key.NumPad1)]
    [InlineData(Key.NumPad5)]
    [InlineData(Key.Decimal)]
    public void In_the_editor_with_num_lock_on_the_keypad_types(Key key)
    {
        Assert.True(TextRouting.ProducesText(key, Map, textEntryActive: true, numLockOn: true, KeyProfile.TextEditing));
    }

    [Theory]
    [InlineData(Key.Left, KeyModifiers.None)]       // keypad 4 with Num Lock off
    [InlineData(Key.Home, KeyModifiers.None)]       // keypad 7 with Num Lock off
    [InlineData(Key.Enter, KeyModifiers.None)]
    [InlineData(Key.Delete, KeyModifiers.None)]
    [InlineData(Key.S, KeyModifiers.Ctrl)]
    [InlineData(Key.M, KeyModifiers.Ctrl)]
    [InlineData(Key.H, KeyModifiers.Ctrl)]
    [InlineData(Key.Divide, KeyModifiers.None)]
    public void In_the_editor_commands_do_not_type(Key key, KeyModifiers modifiers)
    {
        Assert.False(TextRouting.ProducesText(new KeyChord(key, modifiers), Map, textEntryActive: true, numLockOn: false, KeyProfile.TextEditing));
        Assert.False(TextRouting.ProducesText(new KeyChord(key, modifiers), Map, textEntryActive: true, numLockOn: true, KeyProfile.TextEditing));
    }

    [Theory]
    [InlineData(Key.A, KeyModifiers.None)]
    [InlineData(Key.S, KeyModifiers.Ctrl | KeyModifiers.Alt)] // AltGr+S = ș, not Ctrl+S
    [InlineData(Key.Other, KeyModifiers.None)]
    [InlineData(Key.Space, KeyModifiers.None)]
    public void In_the_editor_letters_type(Key key, KeyModifiers modifiers)
    {
        Assert.True(TextRouting.ProducesText(new KeyChord(key, modifiers), Map, textEntryActive: true, numLockOn: false, KeyProfile.TextEditing));
    }

    [Fact]
    public void Keypad_5_with_num_lock_off_arrives_as_clear_and_activates()
    {
        Assert.Equal(Command.Activate, Map.Resolve(Key.Clear));
        Assert.Equal(Command.None, Map.Resolve(Key.Insert)); // keypad 0 with Num Lock off stays unbound
    }

    [Fact]
    public void Clear_opens_the_selected_item()
    {
        var h = new ShellHarness().Start(TestScreens.Fruits());

        Assert.Equal(["Măr. Un măr."], h.Press(Key.Clear));
    }

    private static ShellHarness InTextField() =>
        new ShellHarness().Start(new TextInputScreen("Test", "Scrieți.", (_, _) => ScreenResult.Handled));

    [Fact]
    public void Switching_num_lock_in_a_text_field_is_announced()
    {
        var h = InTextField();

        h.Shell.NumericKeypadModeChanged(numLockOn: true);
        Assert.Equal("Tastatura numerică: cifre.", h.LastSaid);

        h.Shell.NumericKeypadModeChanged(numLockOn: false);
        Assert.Equal("Tastatura numerică: navigare.", h.LastSaid);
    }

    [Fact]
    public void Switching_num_lock_in_a_menu_says_nothing_because_nothing_changes()
    {
        var h = new ShellHarness().Start(TestScreens.Fruits());
        var before = h.Output.Announcements.Count;

        h.Shell.NumericKeypadModeChanged(numLockOn: true);

        Assert.Equal(before, h.Output.Announcements.Count);
    }

    [Fact]
    public void The_num_lock_key_itself_says_nothing()
    {
        var h = InTextField();

        Assert.Empty(h.Press(Key.NumLock, Key.CapsLock));
    }

    [Fact]
    public void Typed_keypad_digits_are_echoed_in_a_text_field()
    {
        var h = InTextField();

        Assert.Equal(["8"], h.Type("8"));
    }
}
