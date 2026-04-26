using System.ComponentModel;
using CaptureImage.Core.Models;
using CaptureImage.ViewModels.Settings;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Windows.UI.Core;

namespace CaptureImage.UI.Controls;

/// <summary>
/// Keyboard-driven rebinder for <see cref="HotkeyBinding"/>. Owns no state itself —
/// it forwards completed combinations to the attached <see cref="HotkeyBindingViewModel"/>
/// and reads validation / conflict flags back to drive its own visibility.
/// </summary>
/// <remarks>
/// <para>
/// WinUI 3's <see cref="VirtualKey"/> values match the Win32 VK codes 1:1, so persistence
/// can carry the integer through unchanged. Modifier state is queried via
/// <see cref="InputKeyboardSource.GetKeyStateForCurrentThread"/> rather than e.KeyModifiers
/// because WinUI 3's <c>KeyRoutedEventArgs</c> doesn't expose a Modifiers field directly.
/// </para>
/// <para>
/// <b>IME caveat:</b> Ctrl/Alt/Win-qualified combos bypass most IMEs (including VN telex
/// and Arabic input), so the recorder should be stable on those layouts. We require at
/// least one modifier for all non-function keys via <see cref="HotkeyBinding.Validate"/>,
/// so the bare-letter IME path doesn't matter in practice.
/// </para>
/// </remarks>
public sealed partial class HotkeyRecorder : UserControl
{
    private HotkeyBindingViewModel? _vm;

    public HotkeyRecorder()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
        }

        _vm = args.NewValue as HotkeyBindingViewModel;

        if (_vm is not null)
        {
            _vm.PropertyChanged += OnVmPropertyChanged;
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HotkeyBindingViewModel.IsRecording)
            && _vm?.IsRecording == true)
        {
            // Grab keyboard focus so the next key press lands on our handler. Posted via
            // the dispatcher queue so the focus move happens after the current click has
            // fully settled — calling Focus() inline can race with WinUI reassigning focus
            // when the Record button collapses.
            DispatcherQueue.TryEnqueue(() => BindingField.Focus(FocusState.Programmatic));
        }
    }

    private void OnFieldKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_vm is null || !_vm.IsRecording) return;

        var modifiers = GetCurrentModifiers();

        // Esc alone cancels without committing.
        if (e.Key == VirtualKey.Escape && modifiers == HotkeyModifiers.None)
        {
            _vm.CancelRecordingCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (IsModifier(e.Key))
        {
            // Pure modifier keypress — wait for a real combination.
            e.Handled = true;
            return;
        }

        // VirtualKey enum values are exactly the Win32 VK codes; cast to uint round-trips
        // cleanly through HotkeyBinding.VirtualKey (which is itself a uint VK).
        var vk = (uint)e.Key;
        _vm.TryCommitRecorded(new HotkeyBinding(modifiers, vk));
        e.Handled = true;
    }

    private static HotkeyModifiers GetCurrentModifiers()
    {
        var mods = HotkeyModifiers.None;
        if (IsKeyDown(VirtualKey.Control))                                               mods |= HotkeyModifiers.Control;
        if (IsKeyDown(VirtualKey.Shift))                                                 mods |= HotkeyModifiers.Shift;
        if (IsKeyDown(VirtualKey.Menu))                                                  mods |= HotkeyModifiers.Alt;
        if (IsKeyDown(VirtualKey.LeftWindows) || IsKeyDown(VirtualKey.RightWindows))     mods |= HotkeyModifiers.Win;
        return mods;
    }

    private static bool IsKeyDown(VirtualKey key) =>
        (InputKeyboardSource.GetKeyStateForCurrentThread(key) & CoreVirtualKeyStates.Down) != 0;

    private static bool IsModifier(VirtualKey key) => key
        is VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl
        or VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift
        or VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu
        or VirtualKey.LeftWindows or VirtualKey.RightWindows;
}
