using System.Runtime.InteropServices;

namespace CaptureImage.UI.Services;

/// <summary>
/// Folder picker via the Win32 <c>IFileOpenDialog</c> COM interface (the modern Vista+
/// dialog set to <c>FOS_PICKFOLDERS</c> mode). Replaces
/// <see cref="Windows.Storage.Pickers.FolderPicker"/> for unpackaged WinUI 3 apps where
/// the WinRT picker throws <c>COMException 0x80004005</c> on
/// <c>PickSingleFolderAsync</c> regardless of <c>InitializeWithWindow</c> being called.
/// IFileOpenDialog is rock-solid across all Windows versions and doesn't need
/// package identity.
/// </summary>
public static class Win32FolderPicker
{
    /// <summary>
    /// Show a folder browser dialog parented to <paramref name="ownerHwnd"/> and return
    /// the chosen folder's full path, or <c>null</c> if the user cancelled.
    /// </summary>
    public static string? PickSingleFolder(IntPtr ownerHwnd, string? initialDirectory = null)
    {
        var dialog = (IFileOpenDialog)new FileOpenDialog();
        try
        {
            // FOS_PICKFOLDERS = 0x20: dialog returns folders instead of files.
            // FOS_FORCEFILESYSTEM = 0x40: only file-system items (no virtual locations
            // like Recycle Bin), so GetDisplayName(FILESYSPATH) always returns a real path.
            // FOS_NOCHANGEDIR = 0x08: don't change the process working directory.
            const uint FOS_PICKFOLDERS = 0x20;
            const uint FOS_FORCEFILESYSTEM = 0x40;
            const uint FOS_NOCHANGEDIR = 0x08;

            dialog.GetOptions(out var options);
            dialog.SetOptions(options | FOS_PICKFOLDERS | FOS_FORCEFILESYSTEM | FOS_NOCHANGEDIR);

            if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
            {
                if (SHCreateItemFromParsingName(initialDirectory, IntPtr.Zero,
                        typeof(IShellItem).GUID, out var folder) == 0 && folder is not null)
                {
                    try
                    {
                        dialog.SetFolder(folder);
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(folder);
                    }
                }
            }

            // Show returns 0 (S_OK) on accept, 0x800704C7 (E_CANCELLED) on cancel,
            // other HRESULTs on error. We treat any non-zero as "no result".
            var hr = dialog.Show(ownerHwnd);
            if (hr != 0) return null;

            dialog.GetResult(out var result);
            if (result is null) return null;
            try
            {
                // SIGDN_FILESYSPATH = 0x80058000: full filesystem path.
                result.GetDisplayName(unchecked((uint)0x80058000), out var path);
                return path;
            }
            finally
            {
                Marshal.ReleaseComObject(result);
            }
        }
        finally
        {
            Marshal.ReleaseComObject(dialog);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = false)]
    private static extern int SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
        IntPtr pbc,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        out IShellItem ppv);

    [ComImport]
    [Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
    private class FileOpenDialog
    {
    }

    [ComImport]
    [Guid("d57c7288-d4ad-4768-be02-9d969532d960")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IFileOpenDialog
    {
        // IModalWindow
        [PreserveSig]
        int Show(IntPtr parent);

        // IFileDialog
        void SetFileTypes(uint cFileTypes, IntPtr rgFilterSpec);
        void SetFileTypeIndex(uint iFileType);
        void GetFileTypeIndex(out uint piFileType);
        void Advise(IntPtr pfde, out uint pdwCookie);
        void Unadvise(uint dwCookie);
        void SetOptions(uint fos);
        void GetOptions(out uint pfos);
        void SetDefaultFolder(IShellItem psi);
        void SetFolder(IShellItem psi);
        void GetFolder(out IShellItem ppsi);
        void GetCurrentSelection(out IShellItem ppsi);
        void SetFileName([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);
        void SetTitle([MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
        void SetOkButtonLabel([MarshalAs(UnmanagedType.LPWStr)] string pszText);
        void SetFileNameLabel([MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
        void GetResult(out IShellItem ppsi);
        void AddPlace(IShellItem psi, int alignment);
        void SetDefaultExtension([MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
        void Close(int hr);
        void SetClientGuid(ref Guid guid);
        void ClearClientData();
        void SetFilter(IntPtr pFilter);

        // IFileOpenDialog
        void GetResults(out IntPtr ppenum);
        void GetSelectedItems(out IntPtr ppsai);
    }

    [ComImport]
    [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItem
    {
        void BindToHandler(IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid bhid,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(uint sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }
}
