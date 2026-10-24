// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.ComponentModel;
using System.Globalization;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Models;
using CaptureImage.Core.Pipeline;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CaptureImage.ViewModels.Tests;

/// <summary>In-memory <see cref="ISettingsStore"/> that applies updates synchronously.</summary>
internal sealed class FakeSettingsStore(AppSettings? initial = null) : ISettingsStore
{
    public AppSettings Current { get; private set; } = initial ?? new AppSettings();

    public event EventHandler? Changed;

    public string SettingsFilePath => @"C:\data\settings.json";

    public int UpdateCount { get; private set; }

    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public void Update(Func<AppSettings, AppSettings> mutator)
    {
        var next = mutator(Current);
        if (ReferenceEquals(next, Current) || next == Current)
        {
            return;
        }
        Current = next;
        UpdateCount++;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>A change made elsewhere (import, another view model).</summary>
    public void ReplaceExternally(AppSettings settings)
    {
        Current = settings;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public Task FlushAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task ExportAsync(string exportPath, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task ImportAsync(string importPath, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task ResetAsync(CancellationToken cancellationToken = default)
    {
        ReplaceExternally(new AppSettings { Culture = Current.Culture });
        return Task.CompletedTask;
    }
}

/// <summary>Runs everything inline, as if every caller were on the UI thread.</summary>
internal sealed class InlineDispatcher : IUIThreadDispatcher
{
    public bool IsOnUIThread => true;

    public void Post(Action action) => action();

    public Task InvokeAsync(Action action)
    {
        action();
        return Task.CompletedTask;
    }

    public Task<T> InvokeAsync<T>(Func<T> func) => Task.FromResult(func());

    public Task InvokeAsync(Func<Task> func) => func();
}

/// <summary>Returns the key itself, so tests assert which text was chosen.</summary>
internal sealed class KeyLocalization : ILocalizationService
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? CultureChanged;

    public CultureInfo CurrentCulture { get; private set; } = CultureInfo.GetCultureInfo("en-US");

    public TextFlowDirection CurrentFlowDirection => TextFlowDirection.LeftToRight;

    public IReadOnlyList<CultureInfo> SupportedCultures { get; } =
        [CultureInfo.GetCultureInfo("en-US"), CultureInfo.GetCultureInfo("vi-VN"), CultureInfo.GetCultureInfo("ar-SA")];

    public string this[string key] => key;

    public void SetCulture(CultureInfo culture)
    {
        CurrentCulture = culture;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        CultureChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>A real <see cref="CaptureCoordinator"/> wired to substitutes, for view models that drive it.</summary>
internal sealed class CoordinatorFixture : IDisposable
{
    public CoordinatorFixture(ISettingsStore settings)
    {
        Settings = settings;
        Hotkeys.Activate(Arg.Any<HotkeySettings>()).Returns(call =>
        {
            var bindings = call.Arg<HotkeySettings>();
            return Enum.GetValues<Core.Capture.CaptureMode>()
                .Select(mode => new HotkeyRegistration(mode, bindings.Get(mode),
                    bindings.Get(mode).IsUnbound ? HotkeyRegistrationStatus.Unbound : HotkeyRegistrationStatus.Registered))
                .ToList();
        });
        Performance.BeginForegroundWork().Returns(Substitute.For<IDisposable>());
        Coordinator = new CaptureCoordinator(
            settings,
            Hotkeys,
            Watcher,
            Targets,
            Substitute.For<ICaptureEngine>(),
            Substitute.For<IDisplayService>(),
            Substitute.For<IRegionSelector>(),
            Substitute.For<IPreviewPresenter>(),
            new CaptureFileWriter([], new FileNameStrategy(_ => false), NullLogger<CaptureFileWriter>.Instance),
            Substitute.For<ICaptureLibrary>(),
            Substitute.For<IClipboardService>(),
            Substitute.For<IPinnedThumbnailHost>(),
            Toasts,
            Substitute.For<ISoundService>(),
            Substitute.For<IShellService>(),
            Performance,
            new KeyLocalization(),
            TimeProvider.System,
            NullLogger<CaptureCoordinator>.Instance);
    }

    public ISettingsStore Settings { get; }

    public IHotkeyService Hotkeys { get; } = Substitute.For<IHotkeyService>();

    public IWindowWatcher Watcher { get; } = Substitute.For<IWindowWatcher>();

    public ITargetCatalog Targets { get; } = Substitute.For<ITargetCatalog>();

    public IToastService Toasts { get; } = Substitute.For<IToastService>();

    public IPerformanceModeService Performance { get; } = Substitute.For<IPerformanceModeService>();

    public CaptureCoordinator Coordinator { get; }

    public void Dispose() => Coordinator.Dispose();
}
