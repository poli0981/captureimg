// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Globalization;
using System.Text;
using AwesomeAssertions;
using CaptureImage.Core.Capture;
using CaptureImage.Core.Models;
using CaptureImage.Core.Pipeline;
using CaptureImage.Core.Tests.TestSupport;
using Xunit;

namespace CaptureImage.Core.Tests.Pipeline;

public class FileNameStrategyTests
{
    private const string Template = "{Game}_{yyyy}-{MM}-{dd}_{HH}-{mm}-{ss}";
    private const string ExpectedStem = "Notepad_2026-04-15_19-30-45";

    private static readonly DateTimeOffset FixedTime = new(2026, 4, 15, 19, 30, 45, TimeSpan.Zero);

    /// <summary>Never touched on disk: every test injects its own existence check.</summary>
    private static readonly string OutputDir = Path.Combine("out", "captures");

    private static GameTarget Notepad(string title = "Notepad", string process = "notepad") =>
        TestTargets.Game(title: title, processName: process, executablePath: @"C:\Windows\System32\notepad.exe");

    private static string Expected(string fileName) => Path.Combine(OutputDir, fileName);

    // ---- ExpandTokens ---------------------------------------------------------------------

    [Fact]
    public void ExpandTokens_ReplacesEveryToken()
    {
        var result = FileNameStrategy.ExpandTokens(
            "{Game}|{Process}|{Mode}|{yyyy}|{MM}|{dd}|{HH}|{mm}|{ss}|{counter}",
            Notepad(title: "Super Mario", process: "mario"),
            CaptureMode.Region,
            FixedTime,
            "_7");

        result.Should().Be("Super Mario|mario|Region|2026|04|15|19|30|45|_7");
    }

    [Theory]
    [InlineData("{Game}", "Super Mario")]
    [InlineData("{Process}", "mario")]
    [InlineData("{Mode}", "Monitor")]
    [InlineData("{yyyy}", "0999")]
    [InlineData("{MM}", "01")]
    [InlineData("{dd}", "02")]
    [InlineData("{HH}", "03")]
    [InlineData("{mm}", "04")]
    [InlineData("{ss}", "05")]
    [InlineData("{counter}", "_3")]
    public void ExpandTokens_EachTokenIsZeroPaddedAndInvariant(string template, string expected)
    {
        var time = new DateTimeOffset(999, 1, 2, 3, 4, 5, TimeSpan.Zero);

        FileNameStrategy.ExpandTokens(template, Notepad("Super Mario", "mario"), CaptureMode.Monitor, time, "_3")
            .Should().Be(expected);
    }

    [Fact]
    public void ExpandTokens_UsesTheCaptureTimeAsGiven()
    {
        // The coordinator passes local time; the name must show that wall clock, not UTC.
        var local = new DateTimeOffset(2026, 4, 15, 23, 30, 45, TimeSpan.FromHours(7));

        FileNameStrategy.ExpandTokens("{dd} {HH}", null, CaptureMode.Window, local, string.Empty).Should().Be("15 23");
    }

    [Fact]
    public void ExpandTokens_WithoutTarget_UsesTheModeName()
    {
        FileNameStrategy.ExpandTokens("{Game}-{Process}", null, CaptureMode.Monitor, FixedTime, string.Empty)
            .Should().Be("Monitor-Monitor");
    }

    [Fact]
    public void ExpandTokens_UntitledWindow_UsesTheProcessName()
    {
        FileNameStrategy.ExpandTokens("{Game}", Notepad(title: "  "), CaptureMode.Window, FixedTime, string.Empty)
            .Should().Be("notepad");
    }

    [Theory]
    [InlineData("{Game}_{unknown}_suffix", "Notepad_{unknown}_suffix")]
    [InlineData("{GAME}{game}{ Game }", "{GAME}{game}{ Game }")]
    [InlineData("{Game", "{Game")]
    [InlineData("Game}", "Game}")]
    [InlineData("{}", "{}")]
    [InlineData("{{Game}}", "{Notepad}")]
    [InlineData("{Game}{", "Notepad{")]
    [InlineData("{Game}{counter}", "Notepad")]
    [InlineData("no tokens", "no tokens")]
    public void ExpandTokens_UnknownOrMalformedTokensStayVerbatim(string template, string expected)
    {
        FileNameStrategy.ExpandTokens(template, Notepad(), CaptureMode.Window, FixedTime, string.Empty).Should().Be(expected);
    }

    // ---- Sanitize ---------------------------------------------------------------------------

    [Theory]
    [InlineData("a<b")]
    [InlineData("a>b")]
    [InlineData("a:b")]
    [InlineData("a\"b")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("a|b")]
    [InlineData("a?b")]
    [InlineData("a*b")]
    public void Sanitize_InvalidCharacter_BecomesUnderscore(string raw)
    {
        FileNameStrategy.Sanitize(raw).Should().Be("a_b");
    }

    [Fact]
    public void Sanitize_RunOfInvalidCharacters_BecomesOneUnderscore()
    {
        FileNameStrategy.Sanitize(@"My<Game>:""/\|?*Name").Should().Be("My_Game_Name");
    }

    [Theory]
    [InlineData("a\u0000b")]
    [InlineData("a\u0001b")]
    [InlineData("a\u001Fb")]
    [InlineData("a\u007Fb")]
    [InlineData("a\u0080b")]
    [InlineData("a\u009Fb")]
    public void Sanitize_ControlCharacter_BecomesUnderscore(string raw)
    {
        FileNameStrategy.Sanitize(raw).Should().Be("a_b");
    }

    [Theory]
    [InlineData("a\u202Eb")] // RIGHT-TO-LEFT OVERRIDE
    [InlineData("a\u202Db")] // LEFT-TO-RIGHT OVERRIDE
    [InlineData("a\u2066b")] // LEFT-TO-RIGHT ISOLATE
    [InlineData("a\u200Bb")] // ZERO WIDTH SPACE
    [InlineData("a\u200Db")] // ZERO WIDTH JOINER
    [InlineData("a\u200Eb")] // LEFT-TO-RIGHT MARK
    [InlineData("a\uFEFFb")] // ZERO WIDTH NO-BREAK SPACE (BOM)
    [InlineData("a\u00ADb")] // SOFT HYPHEN
    public void Sanitize_InvisibleFormattingCharacter_BecomesUnderscore(string raw)
    {
        CharUnicodeInfo.GetUnicodeCategory(raw[1]).Should().Be(UnicodeCategory.Format);

        FileNameStrategy.Sanitize(raw).Should().Be("a_b");
    }

    [Fact]
    public void Sanitize_BidiOverride_CannotDisguiseTheName()
    {
        // "photo<RLO>gnp.exe" renders as "photoexe.png" in Explorer.
        var result = FileNameStrategy.Sanitize("photo\u202Egnp.exe");

        result.Should().Be("photo_gnp.exe");
        result.Should().NotContain("\u202E");
    }

    [Theory]
    [InlineData("A    B\t\tC", "A B C")]
    [InlineData("A \t\r\n B", "A B")]
    [InlineData("  Elden   Ring  ", "Elden Ring")]
    [InlineData("A\u00A0\u3000B", "A B")]  // no-break space, ideographic space
    [InlineData("A\u0085B", "A B")]        // NEXT LINE is whitespace, not a control to escape
    public void Sanitize_WhitespaceRunsCollapseToOneSpace(string raw, string expected)
    {
        FileNameStrategy.Sanitize(raw).Should().Be(expected);
    }

    [Theory]
    [InlineData("A <B", "A B")]
    [InlineData("A< B", "A_B")]
    [InlineData("A <> B", "A B")]
    public void Sanitize_SpacesAndReplacementsShareOneSeparator(string raw, string expected)
    {
        FileNameStrategy.Sanitize(raw).Should().Be(expected);
    }

    [Theory]
    [InlineData("..name..", "name")]
    [InlineData("__name__", "name")]
    [InlineData(" _.name._ ", "name")]
    [InlineData("name.", "name")]
    [InlineData("<name>", "name")]
    public void Sanitize_TrimsSpacesUnderscoresAndDotsAtTheEnds(string raw, string expected)
    {
        FileNameStrategy.Sanitize(raw).Should().Be(expected);
    }

    [Theory]
    [InlineData("Ōkami — 大神")]
    [InlineData("GG 🎮")]
    [InlineData("Half-Life 2 (2004) [v1.0]")]
    public void Sanitize_KeepsOrdinaryUnicode(string raw)
    {
        FileNameStrategy.Sanitize(raw).Should().Be(raw);
    }

    [Theory]
    [InlineData("CON", "CON_")]
    [InlineData("con", "con_")]
    [InlineData("Nul", "Nul_")]
    [InlineData("PRN", "PRN_")]
    [InlineData("AUX", "AUX_")]
    [InlineData("COM0", "COM0_")]
    [InlineData("COM1", "COM1_")]
    [InlineData("com9", "com9_")]
    [InlineData("LPT1", "LPT1_")]
    [InlineData("LPT9", "LPT9_")]
    [InlineData("COM¹", "COM¹_")]
    [InlineData("COM³", "COM³_")]
    [InlineData("LPT¹", "LPT¹_")]
    [InlineData("LPT²", "LPT²_")]
    [InlineData("NUL ", "NUL_")]
    [InlineData("nul.txt", "nul.txt_")]
    [InlineData("con.tar.gz", "con.tar.gz_")]
    [InlineData("AUX .png", "AUX .png_")]
    public void Sanitize_ReservedDeviceName_GetsASuffix(string raw, string expected)
    {
        FileNameStrategy.Sanitize(raw).Should().Be(expected);
    }

    [Theory]
    [InlineData("CONSOLE")]
    [InlineData("COM10")]
    [InlineData("LPT")]
    [InlineData("Con Air")]
    [InlineData("xNUL")]
    [InlineData("NULL")]
    public void Sanitize_NamesThatOnlyResembleDevices_AreUnchanged(string raw)
    {
        FileNameStrategy.Sanitize(raw).Should().Be(raw);
    }

    [Fact]
    public void Sanitize_CapsTheLength()
    {
        FileNameStrategy.Sanitize(new string('a', 200)).Should().Be(new string('a', FileNameStrategy.MaxNameLength));
        FileNameStrategy.Sanitize(new string('b', 120)).Should().HaveLength(120);
        FileNameStrategy.MaxNameLength.Should().Be(120);
    }

    [Fact]
    public void Sanitize_TrimsSeparatorsLeftAtTheCut()
    {
        var raw = new string('a', 119) + " tail";

        FileNameStrategy.Sanitize(raw).Should().Be(new string('a', 119));
    }

    [Fact]
    public void Sanitize_NeverSplitsASurrogatePairAtTheCut()
    {
        // The emoji's high surrogate is character 120; the cap must drop the whole pair.
        var raw = new string('a', 119) + "😀b";

        var result = FileNameStrategy.Sanitize(raw);

        result.Should().Be(new string('a', 119));
        IsWellFormedUtf16(result).Should().BeTrue();
    }

    [Fact]
    public void Sanitize_KeepsASurrogatePairThatFitsExactly()
    {
        var raw = new string('a', 118) + "😀bb";

        var result = FileNameStrategy.Sanitize(raw);

        result.Should().Be(new string('a', 118) + "😀");
        result.Should().HaveLength(120);
        IsWellFormedUtf16(result).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    [InlineData("<<<>>>")]
    [InlineData("...")]
    [InlineData("___")]
    [InlineData("\u202E\u200B")]
    public void Sanitize_NothingLeft_FallsBackToCapture(string raw)
    {
        FileNameStrategy.Sanitize(raw).Should().Be("capture");
    }

    // ---- BuildFilePath ------------------------------------------------------------------------

    [Fact]
    public void BuildFilePath_NoCollision_UsesTheSanitizedTemplate()
    {
        var probed = new List<string>();
        var strategy = new FileNameStrategy(path =>
        {
            probed.Add(path);
            return false;
        });

        var path = strategy.BuildFilePath(OutputDir, Template, Notepad(), CaptureMode.Window, FixedTime, ImageFormat.Png);

        path.Should().Be(Expected(ExpectedStem + ".png"));
        probed.Should().Equal(path);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildFilePath_BlankTemplate_UsesTheDefaultTemplate(string template)
    {
        var strategy = new FileNameStrategy(_ => false);

        var path = strategy.BuildFilePath(OutputDir, template, Notepad(), CaptureMode.Window, FixedTime, ImageFormat.Png);

        FileNameStrategy.DefaultTemplate.Should().Be(Template);
        path.Should().Be(Expected(ExpectedStem + ".png"));
    }

    [Fact]
    public void BuildFilePath_WithoutTarget_NamesTheFileAfterTheMode()
    {
        var strategy = new FileNameStrategy(_ => false);

        var path = strategy.BuildFilePath(OutputDir, Template, null, CaptureMode.Region, FixedTime, ImageFormat.Png);

        path.Should().Be(Expected("Region_2026-04-15_19-30-45.png"));
    }

    [Fact]
    public void BuildFilePath_OneCollision_AppendsCounter1()
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Expected(ExpectedStem + ".png") };
        var strategy = new FileNameStrategy(existing.Contains);

        var path = strategy.BuildFilePath(OutputDir, Template, Notepad(), CaptureMode.Window, FixedTime, ImageFormat.Png);

        path.Should().Be(Expected(ExpectedStem + "_1.png"));
    }

    [Fact]
    public void BuildFilePath_ManyCollisions_CountsUpToTheFirstFreeName()
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Expected(ExpectedStem + ".png"),
            Expected(ExpectedStem + "_1.png"),
            Expected(ExpectedStem + "_2.png"),
            Expected(ExpectedStem + "_3.png"),
        };
        var strategy = new FileNameStrategy(existing.Contains);

        var path = strategy.BuildFilePath(OutputDir, Template, Notepad(), CaptureMode.Window, FixedTime, ImageFormat.Png);

        path.Should().Be(Expected(ExpectedStem + "_4.png"));
    }

    [Fact]
    public void BuildFilePath_CollisionInAnotherFormat_IsNotACollision()
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Expected(ExpectedStem + ".jpg") };
        var strategy = new FileNameStrategy(existing.Contains);

        var path = strategy.BuildFilePath(OutputDir, Template, Notepad(), CaptureMode.Window, FixedTime, ImageFormat.Png);

        path.Should().Be(Expected(ExpectedStem + ".png"));
    }

    [Fact]
    public void BuildFilePath_CounterToken_PlacesTheSuffixInsteadOfAppendingIt()
    {
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Expected("Notepad shot.png"),
            Expected("Notepad_1 shot.png"),
        };
        var strategy = new FileNameStrategy(existing.Contains);

        var first = new FileNameStrategy(_ => false)
            .BuildFilePath(OutputDir, "{Game}{counter} shot", Notepad(), CaptureMode.Window, FixedTime, ImageFormat.Png);
        var third = strategy.BuildFilePath(OutputDir, "{Game}{counter} shot", Notepad(), CaptureMode.Window, FixedTime, ImageFormat.Png);

        first.Should().Be(Expected("Notepad shot.png"));
        third.Should().Be(Expected("Notepad_2 shot.png"));
    }

    [Fact]
    public void BuildFilePath_DirectoryPackedWithNames_FallsBackToAMillisecondTimestamp()
    {
        var strategy = new FileNameStrategy(_ => true);

        var path = strategy.BuildFilePath(OutputDir, Template, Notepad(), CaptureMode.Window, FixedTime, ImageFormat.Png);

        var millis = FixedTime.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        path.Should().Be(Expected($"{ExpectedStem}_{millis}.png"));
    }

    [Theory]
    [InlineData(ImageFormat.Png, ".png")]
    [InlineData(ImageFormat.Jpeg, ".jpg")]
    [InlineData(ImageFormat.Webp, ".webp")]
    [InlineData(ImageFormat.Tiff, ".tiff")]
    [InlineData(ImageFormat.Jxr, ".jxr")]
    public void BuildFilePath_UsesTheFormatsExtension(ImageFormat format, string extension)
    {
        var path = new FileNameStrategy(_ => false)
            .BuildFilePath(OutputDir, "{Game}", Notepad(), CaptureMode.Window, FixedTime, format);

        path.Should().Be(Expected("Notepad" + extension));
    }

    [Theory]
    [InlineData("Doom/Eternal: Part <2>", "Doom_Eternal_Part 2.png")]
    [InlineData(@"..\..\Windows\evil", "Windows_evil.png")]
    [InlineData("CON", "CON_.png")]
    [InlineData("<<>>", "capture.png")]
    public void BuildFilePath_HostileTitles_StayInsideTheDirectory(string title, string expectedFileName)
    {
        var path = new FileNameStrategy(_ => false)
            .BuildFilePath(OutputDir, "{Game}", Notepad(title: title), CaptureMode.Window, FixedTime, ImageFormat.Png);

        path.Should().Be(Expected(expectedFileName));
        Path.GetDirectoryName(path).Should().Be(OutputDir);
    }

    private static bool IsWellFormedUtf16(string value)
    {
        try
        {
            _ = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetByteCount(value);
            return true;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }
}
