// SPDX-License-Identifier: AGPL-3.0-only

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using GameTranslator.Core;

namespace GameTranslator.App.Services;

/// <summary>
/// Windows'ta Chiaki, PlayStation Remote Play veya oyun pencerelerini yakalayan servis.
/// Mac'teki CGWindowListCreateImage mekanizmasının Windows (Win32 / GDI / PrintWindow) karşılığıdır.
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

    /// <summary>
    /// Açık olan pencereler arasında Chiaki, Remote Play, PlayStation veya PS5 penceresini arar.
    /// </summary>
    public static IntPtr FindTargetWindow(out string windowTitle)
    {
        IntPtr found = IntPtr.Zero;
        string foundTitle = string.Empty;

        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd)) return true;

            var sb = new StringBuilder(256);
            GetWindowText(hWnd, sb, 256);
            var title = sb.ToString();

            if (string.IsNullOrWhiteSpace(title)) return true;

            if (title.Contains("Chiaki", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("Remote Play", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("PlayStation", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("PS Remote Play", StringComparison.OrdinalIgnoreCase) ||
                title.Contains("PS5", StringComparison.OrdinalIgnoreCase))
            {
                // GameTranslator'ın kendi penceresini atla
                if (!title.Contains("GameTranslator", StringComparison.OrdinalIgnoreCase))
                {
                    found = hWnd;
                    foundTitle = title;
                    return false; // Bulduk, aramayı bitir
                }
            }

            return true;
        }, IntPtr.Zero);

        windowTitle = foundTitle;
        return found;
    }

    /// <summary>
    /// Hedef pencerenin (veya pencere bulunamazsa birincil ekranın) görüntüsünü BGRA Frame olarak yakalar.
    /// </summary>
    public static Frame? Capture(IntPtr hWnd = default)
    {
        try
        {
            int srcX = 0, srcY = 0, width = 0, height = 0;

            if (hWnd != IntPtr.Zero && IsWindowVisible(hWnd))
            {
                if (GetClientRect(hWnd, out var rect) && rect.Width > 100 && rect.Height > 100)
                {
                    var pt = new POINT();
                    ClientToScreen(hWnd, ref pt);
                    srcX = pt.X;
                    srcY = pt.Y;
                    width = rect.Width;
                    height = rect.Height;
                }
            }

            // Eğer hedef pencere yoksa veya çok küçükse tam ekranı yakala
            if (width <= 0 || height <= 0)
            {
                srcX = 0;
                srcY = 0;
                width = GetSystemMetrics(SM_CXSCREEN);
                height = GetSystemMetrics(SM_CYSCREEN);
            }

            if (width <= 0 || height <= 0) return null;

            // Ekran DC'sini al (tüm masaüstü)
            IntPtr srcDc = GetDC(IntPtr.Zero);
            if (srcDc == IntPtr.Zero) return null;

            IntPtr memDc = CreateCompatibleDC(srcDc);
            IntPtr hBitmap = CreateCompatibleBitmap(srcDc, width, height);
            IntPtr oldBmp = SelectObject(memDc, hBitmap);

            bool captured = false;
            if (hWnd != IntPtr.Zero)
            {
                // Önce PrintWindow ile doğrudan pencereden yakalamayı dene
                captured = PrintWindow(hWnd, memDc, PW_RENDERFULLCONTENT);
                if (!captured)
                {
                    captured = PrintWindow(hWnd, memDc, 0);
                }
            }

            if (!captured)
            {
                // PrintWindow başarısızsa BitBlt ile ekran koordinatlarından kopyala
                BitBlt(memDc, 0, 0, width, height, srcDc, srcX, srcY, SRCCOPY);
            }

            var bi = new BITMAPINFO
            {
                bmiHeader = new BITMAPINFOHEADER
                {
                    biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                    biWidth = width,
                    biHeight = -height, // Üstten alta (top-down) DIB
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = BI_RGB
                }
            };

            var pixels = new byte[width * height * 4];
            GetDIBits(memDc, hBitmap, 0, (uint)height, pixels, ref bi, DIB_RGB_COLORS);

            // Temizlik
            SelectObject(memDc, oldBmp);
            DeleteObject(hBitmap);
            DeleteDC(memDc);
            ReleaseDC(IntPtr.Zero, srcDc);

            return new Frame(pixels, width, height, width * 4);
        }
        catch
        {
            return null;
        }
    }
}
