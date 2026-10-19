// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using AwesomeAssertions;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Library;
using CaptureImage.Core.Models;
using CaptureImage.ViewModels.Library;
using CaptureImage.ViewModels.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CaptureImage.ViewModels.Tests.Library;

public sealed class LibraryViewModelTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private readonly ICaptureLibrary _library = Substitute.For<ICaptureLibrary>();
    private readonly IImageDecoder _decoder = Substitute.For<IImageDecoder>();
    private readonly IClipboardService _clipboard = Substitute.For<IClipboardService>();
    private readonly IOcrService _ocr = Substitute.For<IOcrService>();
    private readonly IToastService _toasts = Substitute.For<IToastService>();
    private readonly IDialogService _dialogs = Substitute.For<IDialogService>();
    private readonly IShellService _shell = Substitute.For<IShellService>();
    private readonly LibraryViewModel _vm;

    public LibraryViewModelTests()
    {
        _library.GetAllAsync(Arg.Any<CancellationToken>()).Returns(
        [
            Record(@"C:\Shots\elden_1.png", "Elden Ring", hdr: true),
            Record(@"C:\Shots\notepad.jpg", "Notepad"),
            Record(@"C:\Shots\elden_2.png", "Elden Ring"),
            Record(@"C:\Shots\region.png", null),
        ]);
        _vm = new LibraryViewModel(_library, _decoder, _shell, _clipboard, _ocr, Substitute.For<IPinnedThumbnailHost>(),
            _toasts, _dialogs, new InlineDispatcher(), new KeyLocalization(), NullLogger<LibraryViewModel>.Instance);
    }

    public void Dispose() => _vm.Dispose();

    [Fact]
    public async Task Refresh_ListsCapturesAndGames()
    {
        await _vm.RefreshCommand.ExecuteAsync(null);

        _vm.Items.Should().HaveCount(4);
        _vm.Games.Should().Equal("Library_AllGames", "Elden Ring", "Notepad");
        _vm.SelectedGame.Should().Be("Library_AllGames");
        _vm.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public async Task Filters_Combine()
    {
        await _vm.RefreshCommand.ExecuteAsync(null);

        _vm.SelectedGame = "Elden Ring";
        _vm.Items.Select(i => i.FileName).Should().Equal("elden_1.png", "elden_2.png");

        _vm.HdrOnly = true;
        _vm.Items.Select(i => i.FileName).Should().Equal("elden_1.png");

        _vm.HdrOnly = false;
        _vm.SelectedGame = "Library_AllGames";
        _vm.SearchText = "notepad";
        _vm.Items.Select(i => i.FileName).Should().Equal("notepad.jpg");
    }

    [Fact]
    public async Task Delete_ConfirmedMovesToTheRecycleBin()
    {
        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectedItem = _vm.Items[1];
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(true);
        _library.DeleteAsync(Arg.Any<CaptureRecord>(), Arg.Any<CancellationToken>()).Returns(true);

        await _vm.DeleteCommand.ExecuteAsync(null);

        await _library.Received(1).DeleteAsync(Arg.Is<CaptureRecord>(r => r.FileName == "notepad.jpg"), Arg.Any<CancellationToken>());
        _vm.Items.Select(i => i.FileName).Should().NotContain("notepad.jpg");
    }

    [Fact]
    public async Task Delete_Declined_KeepsTheFile()
    {
        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectedItem = _vm.Items[0];
        _dialogs.ConfirmAsync(default!, default!, default!, default!).ReturnsForAnyArgs(false);

        await _vm.DeleteCommand.ExecuteAsync(null);

        await _library.DidNotReceiveWithAnyArgs().DeleteAsync(default!, TestContext.Current.CancellationToken);
        _vm.Items.Should().HaveCount(4);
    }

    [Fact]
    public async Task CopyImage_DecodesAndCopies()
    {
        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectedItem = _vm.Items[0];
        var frame = new CapturedFrame(1, 1, 4, [0, 0, 0, 255]);
        _decoder.LoadAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(frame);
        _clipboard.CopyImageAsync(frame, Arg.Any<CancellationToken>()).Returns(true);

        await _vm.CopyImageCommand.ExecuteAsync(null);

        _toasts.Received(1).Show(ToastKind.Success, "Toast_ClipboardCopied", "elden_1.png", null);
    }

    [Fact]
    public async Task CopyImage_UnreadableFile_ShowsAnError()
    {
        await _vm.RefreshCommand.ExecuteAsync(null);
        _vm.SelectedItem = _vm.Items[0];
        _decoder.LoadAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns((CapturedFrame?)null);

        await _vm.CopyImageCommand.ExecuteAsync(null);

        _toasts.Received(1).Show(ToastKind.Error, "Toast_Error", "Library_CopyFailed", null);
    }

    [Fact]
    public async Task OpenHdr_IsOnlyAvailableForHdrCaptures()
    {
        await _vm.RefreshCommand.ExecuteAsync(null);

        _vm.SelectedItem = _vm.Items[1];
        _vm.OpenHdrCommand.CanExecute(null).Should().BeFalse();

        _vm.SelectedItem = _vm.Items[0];
        _vm.OpenHdrCommand.CanExecute(null).Should().BeTrue();
        _vm.OpenHdrCommand.Execute(null);
        _shell.Received(1).Open(@"C:\Shots\elden_1.jxr");
    }

    [Fact]
    public void LibraryChanges_RefreshOnlyWhileVisible()
    {
        _library.Changed += Raise.Event();
        _library.DidNotReceive().GetAllAsync(Arg.Any<CancellationToken>());

        _vm.IsPageActive = true;
        _library.Received(1).GetAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Item_FormatsSizeAndDimensions()
    {
        var item = new CaptureItemViewModel(Record(@"C:\Shots\big.png", "Game") with { SizeBytes = 5 * 1024 * 1024 + 512 * 1024 }, _library);

        item.Dimensions.Should().Be("1920 × 1080");
        item.FormatName.Should().Be("PNG");
        item.SizeText.Should().Contain("5").And.Contain("MB");
    }

    private static CaptureRecord Record(string path, string? game, bool hdr = false) =>
        new(path, Now, 1920, 1080, ImageFormatExtensions.FromExtension(Path.GetExtension(path)) ?? ImageFormat.Png, 2048,
            CaptureMode.Window, game, hdr ? Path.ChangeExtension(path, ".jxr") : null);
}
