using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace OpenD
{
    public enum Ic { Plus, Folder, Pause, Play, Stop, Close, Eye, EyeOff, Gear, Chevron }

    // Все значки рисуются фигурами: без картинок и шрифтов с иконками, чёткие на любом DPI.
    static class Icons
    {
        // insetPct — отступ значка от краёв слота в процентах (больше отступ — мельче значок).
        public static void Draw(Graphics g, Ic ic, Rectangle slot, Color c, int insetPct = 30)
        {
            int m = slot.Width * insetPct / 100;              // внутренний отступ
            Rectangle r = Rectangle.Inflate(slot, -m, -m);
            float w = Math.Max(1.4f, slot.Width / 13f);
            using (SolidBrush b = new SolidBrush(c))
            using (Pen p = new Pen(c, w) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                switch (ic)
                {
                    case Ic.Plus:
                        g.DrawLine(p, r.X, r.Y + r.Height / 2, r.Right, r.Y + r.Height / 2);
                        g.DrawLine(p, r.X + r.Width / 2, r.Y, r.X + r.Width / 2, r.Bottom);
                        break;
                    case Ic.Folder:
                        using (GraphicsPath gp = Theme.Round(new Rectangle(r.X, r.Y + r.Height / 4, r.Width, r.Height * 3 / 4), 2))
                            g.DrawPath(p, gp);
                        g.DrawLines(p, new[] { new Point(r.X, r.Y + r.Height / 4), new Point(r.X, r.Y), new Point(r.X + r.Width * 2 / 5, r.Y), new Point(r.X + r.Width / 2, r.Y + r.Height / 4) });
                        break;
                    case Ic.Pause:
                        int bw = Math.Max(2, r.Width / 3);
                        g.FillRectangle(b, r.X + 1, r.Y, bw, r.Height);
                        g.FillRectangle(b, r.Right - bw - 1, r.Y, bw, r.Height);
                        break;
                    case Ic.Play:
                        g.FillPolygon(b, new[] { new Point(r.X + 2, r.Y - 1), new Point(r.Right + 1, r.Y + r.Height / 2), new Point(r.X + 2, r.Bottom + 1) });
                        break;
                    case Ic.Stop:
                        g.FillRectangle(b, r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2);
                        break;
                    case Ic.Close:
                        g.DrawLine(p, r.X, r.Y, r.Right, r.Bottom);
                        g.DrawLine(p, r.Right, r.Y, r.X, r.Bottom);
                        break;
                    case Ic.Eye:
                    case Ic.EyeOff:
                        Rectangle eye = new Rectangle(slot.X + m - 2, slot.Y + slot.Height * 33 / 100, slot.Width - 2 * m + 4, slot.Height * 34 / 100);
                        g.DrawEllipse(p, eye);
                        int d = Math.Max(3, eye.Height / 2);
                        g.FillEllipse(b, eye.X + (eye.Width - d) / 2, eye.Y + (eye.Height - d) / 2, d, d);
                        if (ic == Ic.EyeOff) g.DrawLine(p, r.X - 1, r.Bottom + 1, r.Right + 1, r.Y - 1);
                        break;
                    case Ic.Gear:
                        Point mid = new Point(slot.X + slot.Width / 2, slot.Y + slot.Height / 2);
                        float rad = r.Width / 2f + 1;
                        for (int i = 0; i < 8; i++)
                        {
                            double a = i * Math.PI / 4;
                            g.DrawLine(p, mid.X + (float)(Math.Cos(a) * (rad - 1)), mid.Y + (float)(Math.Sin(a) * (rad - 1)),
                                mid.X + (float)(Math.Cos(a) * (rad + 2)), mid.Y + (float)(Math.Sin(a) * (rad + 2)));
                        }
                        g.DrawEllipse(p, mid.X - rad + 1, mid.Y - rad + 1, 2 * rad - 2, 2 * rad - 2);
                        g.DrawEllipse(p, mid.X - 2.5f, mid.Y - 2.5f, 5, 5);
                        break;
                    default:   // Chevron вниз
                        int cx = slot.X + slot.Width / 2, cy = slot.Y + slot.Height / 2;
                        g.DrawLines(p, new[] { new Point(cx - 3, cy - 1), new Point(cx, cy + 2), new Point(cx + 3, cy - 1) });
                        break;
                }
            }
        }
    }
}
