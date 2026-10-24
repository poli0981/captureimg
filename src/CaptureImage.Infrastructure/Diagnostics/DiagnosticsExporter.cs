// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Globalization;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using CaptureImage.Core.Abstractions;
using CaptureImage.Core.Paths;
using CaptureImage.Infrastructure.Logging;
using Microsoft.Extensions.Logging;
using Vortice.DXGI;

namespace CaptureImage.Infrastructure.Diagnostics;

/// <summary>
/// Bundles what a bug report needs into one zip: recent log files, the settings file and a
/// system summary (Windows build, .NET, GPUs, monitors with DPI and HDR state). The user's
/// profile path is replaced with <c>%USERPROFILE%</c> in every text file.
/// </summary>
public sealed partial class DiagnosticsExporter(
    AppPaths paths,
    IDisplayService displays,
    IUpdateService updates,
    ILogger<DiagnosticsExporter> logger) : IDiagnosticsExporter
{
    private const int MaxLogFiles = 5;

    private readonly AppPaths _paths = paths;
    private readonly IDisplayService _displays = displays;
    private readonly IUpdateService _updates = updates;
    private readonly ILogger<DiagnosticsExporter> _logger = logger;
    private readonly UserPathRedactionEnricher _redactor = new();

    public string SuggestFileName() =>
        $"CaptureImage-diagnostics-{DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.zip";

    public Task<string> ExportAsync(string destinationPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        return Task.Run(() =>
        {
            var temp = destinationPath + ".tmp";
            File.Delete(temp); // left over by an interrupted export; ZipArchiveMode.Create refuses to overwrite it
            using (var zip = ZipFile.Open(temp, ZipArchiveMode.Create))
            {
                AddText(zip, "system.txt", DescribeSystem());

                if (File.Exists(_paths.SettingsFile))
                {
                    AddText(zip, "settings.json", File.ReadAllText(_paths.SettingsFile));
                }

                if (Directory.Exists(_paths.LogsDirectory))
                {
                    foreach (var log in new DirectoryInfo(_paths.LogsDirectory).EnumerateFiles("*.log")
                                 .OrderByDescending(f => f.LastWriteTimeUtc).Take(MaxLogFiles))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        AddText(zip, "logs/" + log.Name, ReadShared(log.FullName));
                    }
                }
            }

            File.Move(temp, destinationPath, overwrite: true);
            LogExported();
            return destinationPath;
        }, cancellationToken);
    }

    private void AddText(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(_redactor.Redact(content));
    }

    /// <summary>Log files are open for writing by the running app; read them with sharing.</summary>
    private static string ReadShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private string DescribeSystem()
    {
        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"CaptureImage {_updates.CurrentVersion} (Velopack install: {_updates.IsAvailable})");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Generated: {DateTimeOffset.Now:O}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"OS: {RuntimeInformation.OSDescription} ({Environment.OSVersion.Version}) {RuntimeInformation.OSArchitecture}");
        sb.AppendLine(CultureInfo.InvariantCulture, $".NET: {RuntimeInformation.FrameworkDescription} {RuntimeInformation.ProcessArchitecture}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"UI culture: {CultureInfo.CurrentUICulture.Name}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"Processors: {Environment.ProcessorCount}");
        sb.AppendLine();

        sb.AppendLine("Monitors:");
        foreach (var m in _displays.GetMonitors())
        {
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"  {m.DeviceName}: {m.Bounds}, {m.Dpi} DPI ({m.Scale:P0}){(m.IsPrimary ? ", primary" : string.Empty)}, HDR {(m.Hdr.IsHdrActive ? $"on (SDR white {m.Hdr.SdrWhiteNits:0} nits, peak {m.Hdr.MaxLuminanceNits:0} nits)" : "off")}");
        }
        sb.AppendLine();

        sb.AppendLine("Graphics adapters:");
        try
        {
            using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
            for (uint i = 0; factory.EnumAdapters1(i, out var adapter).Success; i++)
            {
                using (adapter)
                {
                    var desc = adapter.Description1;
                    sb.AppendLine(CultureInfo.InvariantCulture, $"  {desc.Description} (VRAM {desc.DedicatedVideoMemory / 1024 / 1024} MB, flags {desc.Flags})");
                }
            }
        }
        catch (SharpGen.Runtime.SharpGenException ex)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"  unavailable: {ex.Message}");
        }

        return sb.ToString();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Diagnostics bundle exported.")]
    private partial void LogExported();
}
