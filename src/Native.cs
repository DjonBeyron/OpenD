using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace OpenD
{
    static class Native
    {
        public const int WM_HOTKEY = 0x0312;
        public const int WM_CLIPBOARDUPDATE = 0x031D;
        public const uint MOD_NOREPEAT = 0x4000;

        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vKey);
        [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint vk);
        [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h, int id);
        [DllImport("user32.dll")] public static extern bool AddClipboardFormatListener(IntPtr h);
        [DllImport("user32.dll")] public static extern bool RemoveClipboardFormatListener(IntPtr h);
        [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int v, int size);

        public static void Style(IntPtr h, bool round)
        {
            try
            {
                int on = 1, corner = 2;
                int border = Theme.Line.R | (Theme.Line.G << 8) | (Theme.Line.B << 16);
                DwmSetWindowAttribute(h, 20, ref on, 4);           // тёмный заголовок
                DwmSetWindowAttribute(h, 34, ref border, 4);       // цвет рамки
                if (round) DwmSetWindowAttribute(h, 33, ref corner, 4);
            }
            catch { }
        }

        public static Icon MakeIcon()
        {
            using (Bitmap b = new Bitmap(32, 32))
            {
                using (Graphics g = Graphics.FromImage(b))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (GraphicsPath p = Theme.Round(new Rectangle(1, 1, 30, 30), 8))
                    using (SolidBrush bg = new SolidBrush(Color.FromArgb(34, 34, 38)))
                    using (Pen edge = new Pen(Theme.Accent, 2))
                    {
                        g.FillPath(bg, p);
                        g.DrawPath(edge, p);
                    }
                    using (Pen a = new Pen(Theme.Accent, 3.2f))
                    {
                        a.StartCap = LineCap.Round; a.EndCap = LineCap.Round; a.LineJoin = LineJoin.Round;
                        g.DrawLine(a, 16, 7, 16, 21);
                        g.DrawLines(a, new[] { new Point(10, 15), new Point(16, 22), new Point(22, 15) });
                        g.DrawLine(a, 9, 26, 23, 26);
                    }
                }
                IntPtr hi = b.GetHicon();
                Icon ic = (Icon)Icon.FromHandle(hi).Clone();
                DestroyIcon(hi);
                return ic;
            }
        }
    }

    static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(18, 18, 20);
        public static readonly Color Panel = Color.FromArgb(27, 27, 31);
        public static readonly Color Hover = Color.FromArgb(38, 38, 43);
        public static readonly Color Line = Color.FromArgb(46, 46, 52);
        public static readonly Color Text = Color.FromArgb(228, 228, 232);
        public static readonly Color Dim = Color.FromArgb(138, 138, 148);
        public static readonly Color Accent = Color.FromArgb(96, 165, 250);
        public static readonly Color Ok = Color.FromArgb(74, 222, 128);
        public static readonly Color Err = Color.FromArgb(248, 113, 113);
        public static readonly Color Warn = Color.FromArgb(234, 179, 8);

        public static GraphicsPath Round(Rectangle r, int rad)
        {
            int d = rad * 2;
            GraphicsPath p = new GraphicsPath();
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
