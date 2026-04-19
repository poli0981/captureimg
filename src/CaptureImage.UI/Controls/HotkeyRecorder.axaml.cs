using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using CaptureImage.Core.Models;
using CaptureImage.ViewModels.Settings;

namespace CaptureImage.UI.Controls;

/// <summary>
/// Keyboard-driven rebinder for <see cref="HotkeyBinding"/>. Owns no state itself —
/// it forwards completed combinations to the attached <see cref="HotkeyBindingViewModel"/>
/// and reads validation / conflict flags back to drive its own visibility.
/// </summary>
/// <remarks>
/// <para>
/// We map <see cref="Key"/> (Avalonia) to the Win32 virtual-key code because the backing
/// <c>HotkeyBinding</c> persists VKs. SharpHook would give us VK codes directly but it
/// lives in Infrastructure and the whole point of this control is to stay reusable from
/// the UI layer without a back-channel to the global hook.
/// </para>
/// <para>
/// <b>IME caveat:</b> Ctrl/Alt/Win-qualified combos bypass most IMEs (including VN telex
/// and Arabic input), so the recorder should be stable on those layouts. A truly bare
/// letter press would route through the IME — we require at least one modifier for all
/// non-function keys via <see cref="HotkeyBinding.Validate"/>, so this doesn't matter in
/// practice.
/// </para>
/// </remarks>
public partial class HotkeyRecorder : UserControl
{
    private HotkeyBindingViewModel? _vm;

    public HotkeyRecorder()
    {
        InitializeComponent();

        DataContextChanged += OnDataContextChanged;
        KeyDown += OnKeyDown;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null)
            _vm.PropertyChanged -= OnVmPropertyChanged;

        _vm = DataContext as HotkeyBindingViewModel;

        if (_vm is not null)
            _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HotkeyBindingViewModel.IsRecording)
            && _vm?.IsRecording == true)
        {
            // Grab keyboard focus so the next key press lands on our handler. Posted via
            // the dispatcher so the focus move happens after the current click has fully
            // settled — calling Focus() inline sometimes races with Avalonia reassigning
            // focus when the Record button collapses.
            Avalonia.Threading.Dispatcher.UIThread.Post(() => BindingField.Focus());
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_vm is null || !_vm.IsRecording) return;

        // Esc alone cancels without committing.
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
        {
            _vm.CancelRecordingCommand.Execute(null);
            e.Handled = true;
            return;
        }

        var vk = AvaloniaKeyToWin32Vk(e.Key);
        if (vk is null)
        {
            // Pure modifier or an unmapped key — wait for a real combination.
            e.Handled = true;
            return;
        }

        var mods = HotkeyModifiers.None;
        if ((e.KeyModifiers & KeyModifiers.Control) != 0) mods |= HotkeyModifiers.Control;
        if ((e.KeyModifiers & KeyModifiers.Alt)     != 0) mods |= HotkeyModifiers.Alt;
        if ((e.KeyModifiers & KeyModifiers.Shift)   != 0) mods |= HotkeyModifiers.Shift;
        if ((e.KeyModifiers & KeyModifiers.Meta)    != 0) mods |= HotkeyModifiers.Win;

        _vm.TryCommitRecorded(new HotkeyBinding(mods, vk.Value));
        e.Handled = true;
    }

    /// <summary>
    /// Map Avalonia's platform-neutral <see cref="Key"/> enum to Win32 virtual-key codes
    /// (<c>VK_*</c>). Returns <c>null</c> for pure modifier keys and anything we don't
    /// bind for a capture hotkey — e.g. media keys, IME-specific modes.
    /// </summary>
    private static uint? AvaloniaKeyToWin32Vk(Key key)
    {
        // Letters A-Z → 0x41..0x5A. Avalonia keeps these contiguous.
        if (key >= Key.A && key <= Key.Z)
            return 0x41u + (uint)(key - Key.A);

        // Top-row digits 0-9 → 0x30..0x39.
        if (key >= Key.D0 && key <= Key.D9)
            return 0x30u + (uint)(key - Key.D0);

        // Function keys F1-F24 → 0x70..0x87.
        if (key >= Key.F1 && key <= Key.F24)
            return 0x70u + (uint)(key - Key.F1);

        // Numpad 0-9 → 0x60..0x69.
        if (key >= Key.NumPad0 && key <= Key.NumPad9)
            return 0x60u + (uint)(key - Key.NumPad0);

        return key switch
        {
            Key.Tab => 0x09u,
            Key.Return => 0x0Du,
            Key.Space => 0x20u,
            Key.Back => 0x08u,
            Key.Escape => 0x1Bu,
            Key.Insert => 0x2Du,
            Key.Delete => 0x2Eu,
            Key.Home => 0x24u,
            Key.End => 0x23u,
            Key.PageUp => 0x21u,
            Key.PageDown => 0x22u,
            Key.Left => 0x25u,
            Key.Up => 0x26u,
            Key.Right => 0x27u,
            Key.Down => 0x28u,
            Key.PrintScreen => 0x2Cu,
            Key.Pause => 0x13u,
            Key.Multiply => 0x6Au,
            Key.Add => 0x6Bu,
            Key.Subtract => 0x6Du,
            Key.Decimal => 0x6Eu,
            Key.Divide => 0x6Fu,
            // Anything else (modifiers, IME modes, media keys) → not bindable.
            _ => null,
        };
    }
}
