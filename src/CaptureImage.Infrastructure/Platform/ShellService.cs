// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.ComponentModel;
using System.Diagnostics;
using CaptureImage.Core.Abstractions;
using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.UI.Shell;

namespace CaptureImage.Infrastructure.Platform;

/// <summary>Explorer, default-program and Recycle Bin integration.</summary>
public sealed partial class ShellService(ILogger<ShellService> logger) : IShellService
{
    private const uint FoDelete = 0x0003;
    private const ushort FofSilent = 0x0004;
    private const ushort FofNoConfirmation = 0x0010;
    private const ushort FofAllowUndo = 0x0040;
    private const ushort FofNoErrorUi = 0x0400;

    private static readonly string ExplorerPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");

    private readonly ILogger<ShellService> _logger = logger;

    public bool Open(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || (!File.Exists(path) && !Directory.Exists(path)))
        {
            return false;
        }

        return Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    public bool RevealInExplorer(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return false;
        }

        if (!File.Exists(filePath))
        {
            var folder = Path.GetDirectoryName(filePath);
            return folder is not null && Open(folder);
        }

        // Absolute path to explorer.exe: never resolve an executable through PATH.
        var start = new ProcessStartInfo(ExplorerPath) { UseShellExecute = false };
        start.ArgumentList.Add("/select," + filePath);
        return Start(start);
    }

    public bool OpenUrl(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (url.Scheme != Uri.UriSchemeHttps && url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeMailto)
        {
            return false;
        }

        return Start(new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true });
    }

    public unsafe bool MoveToRecycleBin(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return false;
        }

        // SHFileOperation needs a double-null-terminated list.
        var from = Path.GetFullPath(path) + "\0\0";
        fixed (char* pFrom = from)
        {
            var operation = new SHFILEOPSTRUCTW
            {
                wFunc = FoDelete,
                pFrom = pFrom,
                fFlags = FofAllowUndo | FofNoConfirmation | FofSilent | FofNoErrorUi,
            };
            var result = PInvoke.SHFileOperation(ref operation);
            if (result != 0 || operation.fAnyOperationsAborted)
            {
                LogRecycleFailed(result, Path.GetFileName(path));
                return false;
            }
        }
        return true;
    }

    private bool Start(ProcessStartInfo start)
    {
        try
        {
            using var process = Process.Start(start);
            return true;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or FileNotFoundException)
        {
            LogStartFailed(ex, start.FileName);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not open {Target}.")]
    private partial void LogStartFailed(Exception exception, string target);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Moving {FileName} to the Recycle Bin failed (code {Code}).")]
    private partial void LogRecycleFailed(int code, string fileName);
}
