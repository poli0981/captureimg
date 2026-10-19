// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using AwesomeAssertions;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Errors;
using CaptureImage.Core.Library;
using CaptureImage.Core.Models;
using CaptureImage.Core.Tests.TestSupport;
using Xunit;

namespace CaptureImage.Core.Tests.Models;

/// <summary>Small behaviour on the records that flow through a capture.</summary>
public class CaptureRecordsTests
{
    [Theory]
    [InlineData("Elden Ring", "eldenring", "Elden Ring")]
    [InlineData("", "eldenring", "eldenring")]
    [InlineData("   ", "eldenring", "eldenring")]
    public void GameTarget_DisplayName_PrefersTheWindowTitle(string title, string process, string expected)
    {
        TestTargets.Game(title: title, processName: process).DisplayName.Should().Be(expected);
    }

    [Fact]
    public void GameTarget_IsSteamGame_WhenSteamInfoIsKnown()
    {
        TestTargets.Game().IsSteamGame.Should().BeFalse();
        TestTargets.Game(steam: new SteamAppInfo(1245620, "ELDEN RING", "ELDEN RING")).IsSteamGame.Should().BeTrue();
    }

    [Fact]
    public void CaptureRecord_FileNameAndHdrFlag()
    {
        var image = Path.Combine("shots", "Elden Ring_2026-10-19.png");
        var record = new CaptureRecord(image, DateTimeOffset.UnixEpoch, 1920, 1080, ImageFormat.Png, 1234, CaptureMode.Window, "Elden Ring");

        record.FileName.Should().Be("Elden Ring_2026-10-19.png");
        record.HasHdr.Should().BeFalse();
        (record with { HdrFilePath = string.Empty }).HasHdr.Should().BeFalse();
        (record with { HdrFilePath = Path.ChangeExtension(image, ".jxr") }).HasHdr.Should().BeTrue();
    }

    [Theory]
    [InlineData(CaptureOutcomeKind.Saved, true)]
    [InlineData(CaptureOutcomeKind.Copied, true)]
    [InlineData(CaptureOutcomeKind.Cancelled, false)]
    [InlineData(CaptureOutcomeKind.Discarded, false)]
    [InlineData(CaptureOutcomeKind.Busy, false)]
    [InlineData(CaptureOutcomeKind.Failed, false)]
    public void CaptureOutcome_IsSuccess_OnlyWhenSomethingWasKept(CaptureOutcomeKind kind, bool expected)
    {
        new CaptureOutcome(kind, CaptureMode.Region).IsSuccess.Should().Be(expected);
    }

    [Fact]
    public void CaptureException_CarriesTheErrorCode()
    {
        var inner = new IOException("disk full");

        var plain = new CaptureException(CaptureError.TargetGone, "gone");
        var wrapped = new CaptureException(CaptureError.FileWriteFailure, "cannot write", inner);

        plain.ErrorCode.Should().Be(CaptureError.TargetGone);
        plain.Message.Should().Be("gone");
        plain.InnerException.Should().BeNull();
        wrapped.ErrorCode.Should().Be(CaptureError.FileWriteFailure);
        wrapped.InnerException.Should().BeSameAs(inner);
    }
}
