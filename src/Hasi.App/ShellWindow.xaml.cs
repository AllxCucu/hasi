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
    public event EventHandler<KeyChord>? KeyPressed;

    /// <summary>Raised when something other than HASI tries to close the window (taskbar, Alt+F4 outside our handler).</summary>
    public event EventHandler? CloseRequested;

    /// <summary>Set once the user has confirmed exit, so closing is no longer intercepted.</summary>
    public bool AllowClose { get; set; }

    /// <summary>True when this window is the one Windows sends key presses to.</summary>
    public bool HasKeyboard => GetForegroundWindow() == new WindowInteropHelper(this).Handle;

    public void ShowTitle(string title) => ScreenTitle.Text = title;

    public void ShowMessage(string message) => Message.Text = message;

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        // Every key belongs to HASI: no menu activation on Alt, no focus moves on Tab.
        e.Handled = true;
        KeyPressed?.Invoke(this, KeyTranslator.Translate(e));
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
