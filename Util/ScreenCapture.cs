using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace LAHEE.Util;

[SupportedOSPlatform("windows")]
public class ScreenCapture {
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)]
    private static extern IntPtr GetDesktopWindow();

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowRect(IntPtr hWnd, ref Rect rect);

    public static Bitmap CaptureDesktop() {
        return CaptureWindow(GetDesktopWindow());
    }

    public static Bitmap CaptureActiveWindow() {
        return CaptureWindow(GetForegroundWindow());
    }

    private static Bitmap CaptureWindow(IntPtr handle) {
        Rect rect = new Rect();
        GetWindowRect(handle, ref rect);
        Rectangle bounds = new Rectangle(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
        Bitmap result = new Bitmap(bounds.Width, bounds.Height);

        using (Graphics graphics = Graphics.FromImage(result)) {
            graphics.CopyFromScreen(new Point(bounds.Left, bounds.Top), Point.Empty, bounds.Size);
        }

        return result;
    }
}