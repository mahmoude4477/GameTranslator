// SPDX-License-Identifier: AGPL-3.0-only

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using GameTranslator.Core;

namespace GameTranslator.App.Services;

/// <summary>
/// خدمة تلتقط بأمان نوافذ Chiaki أو PlayStation Remote Play على Windows.
/// تلتقط فقط النوافذ التابعة لعمليات اللعب عن بُعد، ولا تلتقط نوافذ سطح المكتب أو مستكشف الملفات أبدا.
/// </summary>
public static class WindowCaptureService
{
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdc, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, int dwRop);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines, byte[] lpvBits, ref BITMAPINFO lpbi, uint uUsage);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;
    private const int SRCCOPY = 0x00CC0020;
    private const int DIB_RGB_COLORS = 0;
    private const int BI_RGB = 0;
    private const uint PW_RENDERFULLCONTENT = 0x00000002;
    private const int SW_RESTORE = 9;

    private static readonly HashSet<string> RemotePlayProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "chiaki",
        "chiaki-ng",
        "RemotePlay",
        "PlayStationRemotePlay"
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        public uint bmiColors;
    }

    public static void RestoreAndActivate(IntPtr hWnd)
    {
        try
        {
            if (hWnd != IntPtr.Zero)
            {
                if (IsIconic(hWnd))
                {
                    ShowWindow(hWnd, SW_RESTORE);
                }
            }
        }
        catch { }
    }

    /// <summary>
    /// يبحث فقط عن نوافذ اللعبة المفتوحة التابعة لعمليات Chiaki أو Sony Remote Play.
    /// لا يختار نوافذ Explorer أو المتصفح أو سطح المكتب هدفا أبدا.
    /// </summary>
    public static IntPtr FindTargetWindow(out string windowTitle)
    {
        IntPtr found = IntPtr.Zero;
        string foundTitle = string.Empty;
        int bestScore = -1;

        EnumWindows((hWnd, _) =>
        {
            try
            {
                if (!IsWindowVisible(hWnd) || IsIconic(hWnd)) return true;

                GetWindowThreadProcessId(hWnd, out var pid);
                if (pid == 0 || pid == Environment.ProcessId) return true;

                string procName;
                try
                {
                    using var proc = Process.GetProcessById((int)pid);
                    procName = proc.ProcessName;
                }
                catch
                {
                    return true;
                }

                // اقبل فقط عمليات PS Remote Play المعتمدة
                if (!RemotePlayProcessNames.Contains(procName)) return true;

                if (!GetClientRect(hWnd, out var rect) || rect.Width < 250 || rect.Height < 150)
                    return true;

                var sb = new StringBuilder(256);
                GetWindowText(hWnd, sb, 256);
                var title = sb.ToString();

                // أعطِ الأولوية لنافذة البث/اللعبة
                int score = 10;
                if (title.Contains("Stream", StringComparison.OrdinalIgnoreCase)) score += 50;
                if (title.Contains("PlayStation", StringComparison.OrdinalIgnoreCase)) score += 30;
                if (title.Contains("PS5", StringComparison.OrdinalIgnoreCase)) score += 30;
                if (rect.Width >= 1200 || rect.Height >= 700) score += 20;

                if (score > bestScore)
                {
                    bestScore = score;
                    found = hWnd;
                    foundTitle = string.IsNullOrWhiteSpace(title) ? procName : title;
                }
            }
            catch { }

            return true;
        }, IntPtr.Zero);

        windowTitle = foundTitle;
        return found;
    }

    /// <summary>
    /// يلتقط صورة نافذة Chiaki أو Remote Play الهدف على هيئة Frame بصيغة BGRA.
    /// يعيد null إن لم يُعثر على النافذة أو كانت غير صالحة؛ ولا يلتقط سطح المكتب أبدا.
    /// </summary>
    public static Frame? Capture(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero) return null;

        try
        {
            if (!IsWindowVisible(hWnd) || IsIconic(hWnd)) return null;

            if (!GetClientRect(hWnd, out var rect) || rect.Width < 100 || rect.Height < 100)
                return null;

            var pt = new POINT();
            ClientToScreen(hWnd, ref pt);
            int srcX = pt.X;
            int srcY = pt.Y;
            int width = rect.Width;
            int height = rect.Height;

            int screenW = GetSystemMetrics(SM_CXSCREEN);
            int screenH = GetSystemMetrics(SM_CYSCREEN);
            if (width <= 0 || height <= 0 || srcX >= screenW || srcY >= screenH) return null;

            IntPtr srcDc = GetDC(IntPtr.Zero);
            if (srcDc == IntPtr.Zero) return null;

            IntPtr memDc = CreateCompatibleDC(srcDc);
            IntPtr hBitmap = CreateCompatibleBitmap(srcDc, width, height);
            IntPtr oldBmp = SelectObject(memDc, hBitmap);

            bool captured = false;
            try
            {
                captured = PrintWindow(hWnd, memDc, PW_RENDERFULLCONTENT);
            }
            catch { }

            if (!captured)
            {
                try
                {
                    captured = PrintWindow(hWnd, memDc, 0);
                }
                catch { }
            }

            var bi = new BITMAPINFO
            {
                bmiHeader = new BITMAPINFOHEADER
                {
                    biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                    biWidth = width,
                    biHeight = -height, // top-down
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = BI_RGB
                }
            };

            var pixels = new byte[width * height * 4];
            GetDIBits(memDc, hBitmap, 0, (uint)height, pixels, ref bi, DIB_RGB_COLORS);

            // إن أعطى PrintWindow بكسلات سوداء تماما (شائع في نوافذ DirectX/OpenGL)
            // وكانت النافذة ظاهرة على الشاشة فنفّذ BitBlt مباشرة من DC الشاشة
            if (IsFrameBlack(pixels, width, height) && srcX >= 0 && srcY >= 0 && srcX + width <= screenW && srcY + height <= screenH)
            {
                BitBlt(memDc, 0, 0, width, height, srcDc, srcX, srcY, SRCCOPY);
                GetDIBits(memDc, hBitmap, 0, (uint)height, pixels, ref bi, DIB_RGB_COLORS);
            }

            SelectObject(memDc, oldBmp);
            DeleteObject(hBitmap);
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, srcDc);

            if (IsFrameBlack(pixels, width, height))
            {
                return null;
            }

            return new Frame(pixels, width, height, width * 4);
        }
        catch
        {
            return null;
        }
    }

    private static bool IsFrameBlack(byte[] pixels, int width, int height)
    {
        if (pixels == null || pixels.Length < 16) return true;
        int step = Math.Max(1, (width * height) / 40);
        for (int i = 0; i < width * height; i += step)
        {
            int idx = i * 4;
            if (idx + 2 < pixels.Length)
            {
                if (pixels[idx] > 5 || pixels[idx + 1] > 5 || pixels[idx + 2] > 5)
                {
                    return false;
                }
            }
        }
        return true;
    }
}
