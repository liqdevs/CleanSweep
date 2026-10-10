using System;
using System.Runtime.InteropServices;

namespace CleanSweep.Core.Utils;

public static class ShellUtils
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct SHQUERYRBINFO
    {
        public int cbSize;
        public long i64Size;
        public long i64NumItems;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    private const uint SHERB_NOCONFIRMATION = 0x00000001;
    private const uint SHERB_NOPROGRESSUI = 0x00000002;
    private const uint SHERB_NOSOUND = 0x00000004;

    public static (long sizeBytes, int itemCount) RecycleBinSize(string? drive = null)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return (0, 0);
        }

        var info = new SHQUERYRBINFO();
        info.cbSize = Marshal.SizeOf<SHQUERYRBINFO>();

        try
        {
            int hresult = SHQueryRecycleBin(drive, ref info);
            if (hresult == 0)
            {
                return (info.i64Size, (int)info.i64NumItems);
            }
        }
        catch
        {
            // Failed to query recycle bin
        }

        return (0, 0);
    }

    public static bool EmptyRecycleBin(string? drive = null)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false;
        }

        uint flags = SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND;
        try
        {
            int hresult = SHEmptyRecycleBin(IntPtr.Zero, drive, flags);
            // 0 = S_OK, unchecked((int)0x8000FFFF) (-2147418113) = E_UNEXPECTED / already empty
            return hresult == 0 || hresult == unchecked((int)0x8000FFFF);
        }
        catch
        {
            return false;
        }
    }
}

