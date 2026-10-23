using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Hasi.Core.Input;

namespace Hasi.App;

/// <summary>
/// The only window. It shows the current screen title and the last message in large text,
/// and forwards every key press. It contains no logic of its own.
/// </summary>
public partial class ShellWindow : Window
{
    public ShellWindow()
    {
        InitializeComponent();
    }

    /// <summary>Raised for every key press, already translated to a platform-neutral chord.</summary>
    public event EventHandler<KeyPress>? KeyPressed;

    /// <summary>Raised when something other than HASI tries to close the window (taskbar, Alt+F4 outside our handler).</summary>
    public event EventHandler? CloseRequested;

    /// <summary>Set once the user has confirmed exit, so closing is no longer intercepted.</summary>
    public bool AllowClose { get; set; }

    /// <summary>True when this window is the one Windows sends key presses to.</summary>
    public bool HasKeyboard => GetForegroundWindow() == new WindowInteropHelper(this).Handle;

    public void ShowTitle(string title) => ScreenTitle.Text = title;

    public void ShowMessage(string message) => Message.Text = message;

    /// <summary>Raised for a key that may produce a typed character; the character follows in <see cref="CharacterTyped"/>.</summary>
    public event EventHandler<KeyChord>? TextKeyPressed;

    /// <summary>Raised for each character typed, in the user's keyboard layout (ă, î, â, ș, ț included).</summary>
    public event EventHandler<char>? CharacterTyped;

    /// <summary>Whether a key press should type a character instead of being a command (see TextRouting).</summary>
    public Func<KeyChord, bool> ProducesText { get; set; } = _ => false;

    /// <summary>Raised after Num Lock is switched, with its new state.</summary>
    public event EventHandler<bool>? NumLockChanged;

    /// <summary>Current Num Lock state.</summary>
    public static bool NumLockOn => Keyboard.IsKeyToggled(System.Windows.Input.Key.NumLock);

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        var chord = KeyTranslator.Translate(e);
        if (ProducesText(chord))
        {
            // Left unhandled so Windows turns it into a character in the current keyboard layout.
            TextKeyPressed?.Invoke(this, chord);
            return;
        }
        // Every other key belongs to HASI: no menu activation on Alt, no focus moves on Tab.
        // A handled key also produces no character (WPF skips TranslateMessage), so NumPad 8 never types "8".
        e.Handled = true;
        KeyPressed?.Invoke(this, new KeyPress(chord, e.IsRepeat));
    }

    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        // The toggle state is read on key up, when Windows has certainly updated it.
        if (e.Key == System.Windows.Input.Key.NumLock) NumLockChanged?.Invoke(this, NumLockOn);
    }

    protected override void OnPreviewTextInput(TextCompositionEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        e.Handled = true;
        foreach (var c in e.Text)
        {
            if (!char.IsControl(c)) CharacterTyped?.Invoke(this, c);
        }
    }

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr GetForegroundWindow();

    protected override void OnClosing(CancelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (!AllowClose)
        {
            e.Cancel = true;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        base.OnClosing(e);
    }
}

/// <param name="IsRepeat">Sent by keyboard auto-repeat while the key is held down.</param>
public readonly record struct KeyPress(KeyChord Chord, bool IsRepeat);
