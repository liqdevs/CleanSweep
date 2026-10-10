using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace CleanSweep;

internal static class WindowChromeTheme
{
    private const int DarkModeAttribute = 20;
    private const int LegacyDarkModeAttribute = 19;
    private const int BorderColorAttribute = 34;
    private const int CaptionColorAttribute = 35;
    private const int TextColorAttribute = 36;
    private const uint DarkCaptionColor = 0x001B1512;
    private const uint LightCaptionTextColor = 0x00EEE9E7;
    private const uint CaptionBorderColor = 0x003A2F2A;

    public static void Apply(Window window)
    {
        window.SourceInitialized += (_, _) =>
        {
            IntPtr handle = new WindowInteropHelper(window).Handle;
            uint enabled = 1;
            if (SetWindowAttribute(handle, DarkModeAttribute, enabled) != 0)
            {
                SetWindowAttribute(handle, LegacyDarkModeAttribute, enabled);
            }

            SetWindowAttribute(handle, CaptionColorAttribute, DarkCaptionColor);
            SetWindowAttribute(handle, TextColorAttribute, LightCaptionTextColor);
            SetWindowAttribute(handle, BorderColorAttribute, CaptionBorderColor);
        };
    }

    private static int SetWindowAttribute(IntPtr handle, int attribute, uint value) =>
        DwmSetWindowAttribute(handle, attribute, ref value, sizeof(uint));

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd,
        int attribute,
        ref uint value,
        int valueSize);
}
