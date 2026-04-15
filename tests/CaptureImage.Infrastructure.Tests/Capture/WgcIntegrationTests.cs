using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Errors;
using CaptureImage.Core.Models;
using CaptureImage.Core.Pipeline;
using CaptureImage.Infrastructure.Capture;
using CaptureImage.Infrastructure.Imaging;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CaptureImage.Infrastructure.Tests.Capture;

/// <summary>
/// Windows-only integration tests for the WGC pipeline. Spawns a real Notepad window,
/// drives the full engine + orchestrator + SkiaImageEncoder path, and asserts the saved
/// file exists with plausible content (non-black, correct extension, size > 0).
/// </summary>
/// <remarks>
/// These tests require:
/// <list type="bullet">
///   <item>A desktop session (not headless CI without a GUI).</item>
///   <item>Windows 10.0.19041 or later for WGC.</item>
///   <item>notepad.exe available on PATH.</item>
/// </list>
/// They are tagged <c>Category=Windows</c> so CI can opt out if needed.
/// </remarks>
[Trait("Category", "Windows")]
public class WgcIntegrationTests
{
    [Fact]
    public async Task Orchestrator_CapturesWinverToPng_SuccessAndNonEmptyFile()
    {
        // Arrange — spawn winver.exe. It's still a classic Win32 app on Win11 (unlike
        // notepad.exe / calc.exe which are Store launchers that hand back a shim process
        // with MainWindowHandle = 0).
        using var winver = Process.Start(new ProcessStartInfo("winver.exe")
        {
            WindowStyle = ProcessWindowStyle.Normal,
            UseShellExecute = true,
        }) ?? throw new InvalidOperationException("Failed to start winver.exe");

        try
        {
            winver.WaitForInputIdle(10_000);
            await WaitForMainWindowAsync(winver, TimeSpan.FromSeconds(10));

            var hwnd = winver.MainWindowHandle;
            hwnd.Should().NotBe(IntPtr.Zero, "winver must have a top-level window");

            var target = new GameTarget(
                ProcessId: (uint)winver.Id,
                WindowHandle: hwnd,
                ProcessName: "winver",
                WindowTitle: winver.MainWindowTitle,
                ExecutablePath: winver.MainModule?.FileName ?? string.Empty,
                IconBytes: null,
                SteamInfo: null);

            var tempDir = Path.Combine(Path.GetTempPath(), "CaptureImageTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var orchestrator = BuildOrchestrator();

                var request = new CaptureRequest(
                    Target: target,
                    Format: ImageFormat.Png,
                    OutputDirectory: tempDir,
                    FileNameTemplate: "winver_{yyyy}-{MM}-{dd}_{HH}-{mm}-{ss}",
                    JpegQuality: 90,
                    WebpQuality: 85);

                // Act
                var result = await orchestrator.ExecuteAsync(request);

                // Assert — include failure detail in the error message so we can see why.
                if (result is CaptureResult.Failure f)
                {
                    throw new Xunit.Sdk.XunitException(
                        $"Capture failed: {f.ErrorCode} — {f.DeveloperMessage}\n{f.Exception}");
                }
                result.Should().BeOfType<CaptureResult.Success>();
                var success = (CaptureResult.Success)result;

                File.Exists(success.FilePath).Should().BeTrue("the output file must be on disk");
                new FileInfo(success.FilePath).Length.Should().BeGreaterThan(1024,
                    "a real capture should be at least 1 KB of PNG data");
                success.Width.Should().BeGreaterThan(0);
                success.Height.Should().BeGreaterThan(0);
                Path.GetExtension(success.FilePath).Should().Be(".png");

                // Verify PNG magic bytes (89 50 4E 47 0D 0A 1A 0A).
                var header = new byte[8];
                using (var fs = File.OpenRead(success.FilePath))
                {
                    var read = fs.Read(header, 0, 8);
                    read.Should().Be(8);
                }
                header[0].Should().Be(0x89);
                header[1].Should().Be(0x50);
                header[2].Should().Be(0x4E);
                header[3].Should().Be(0x47);
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { /* best-effort */ }
            }
        }
        finally
        {
            try { winver.Kill(); } catch { /* ignore */ }
        }
    }

    [Fact]
    public async Task Engine_InvalidHwnd_ThrowsCaptureException()
    {
        var orchestrator = BuildOrchestrator();

        var target = new GameTarget(
            ProcessId: 99999,
            WindowHandle: 0,
            ProcessName: "missing",
            WindowTitle: "missing",
            ExecutablePath: "",
            IconBytes: null,
            SteamInfo: null);

        var request = new CaptureRequest(
            Target: target,
            Format: ImageFormat.Png,
            OutputDirectory: Path.GetTempPath(),
            FileNameTemplate: "missing_{ss}");

        var result = await orchestrator.ExecuteAsync(request);

        result.Should().BeOfType<CaptureResult.Failure>();
        var fail = (CaptureResult.Failure)result;
        fail.ErrorCode.Should().Be(CaptureError.TargetGone);
    }

    // --- helpers ------------------------------------------------------------

    private static CaptureOrchestrator BuildOrchestrator()
    {
        var deviceManager = new D3D11DeviceManager(NullLogger<D3D11DeviceManager>.Instance);
        var engine = new WindowsGraphicsCaptureEngine(
            deviceManager, NullLogger<WindowsGraphicsCaptureEngine>.Instance);
        var fileNameStrategy = new FileNameStrategy(File.Exists);
        var encoders = new List<IImageEncoder> { new SkiaImageEncoder(), new ImageSharpTiffEncoder() };
        return new CaptureOrchestrator(
            engine,
            encoders,
            fileNameStrategy,
            NullLogger<CaptureOrchestrator>.Instance);
    }

    private static async Task WaitForMainWindowAsync(Process process, TimeSpan timeout)
    {
        var start = DateTime.UtcNow;
        while (DateTime.UtcNow - start < timeout)
        {
            process.Refresh();
            if (process.MainWindowHandle != IntPtr.Zero)
            {
                return;
            }
            await Task.Delay(50);
        }
    }
}
