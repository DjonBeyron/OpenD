using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace OpenD
{
    public enum Act { Folder, Pause, Resume, Stop, Delete }

    // Список загрузок, рисуется вручную (без ListView — компактно и в нашей теме).
    // В каждой строке справа внизу кнопки: пауза/продолжить, стоп, удалить.
    class QueueList : Control
    {
        readonly Engine eng;
        readonly Font font = new Font("Segoe UI", 9.5f), small = new Font("Segoe UI", 8.25f);
        int scroll, hot = -1;
        Item[] view = new Item[0];
        Item hotItem; Act? hotAct;

        public event Action<Item, Act> Command;

        public QueueList(Engine e)
        {
            eng = e;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
        }

        float Dpi96 { get { return DeviceDpi / 96f; } }
        int RowH { get { return (int)(78 * Dpi96); } }
        int BtnS { get { return (int)(22 * Dpi96); } }

        public void Refresh2()
        {
            view = eng.Snapshot().OrderByDescending(i => i.Added).ToArray();
            int max = Math.Max(0, view.Length * RowH - Height);
            if (scroll > max) scroll = max;
            Invalidate();
        }

        public Item ItemAt(Point p)
        {
            int i = (p.Y + scroll) / RowH;
            return i >= 0 && p.Y + scroll >= 0 && i < view.Length ? view[i] : null;
        }

        static Act[] Actions(Item it)
        {
            switch (it.State)
            {
                case St.Active:
                case St.Queued: return new[] { Act.Folder, Act.Pause, Act.Stop, Act.Delete };
                case St.Paused: return new[] { Act.Folder, Act.Resume, Act.Stop, Act.Delete };
                case St.Done: return new[] { Act.Folder, Act.Delete };
                default: return new[] { Act.Folder, Act.Resume, Act.Delete };   // ошибка / остановлено
            }
        }

        // Прямоугольники кнопок строки: последняя (Удалить) у правого края.
        Rectangle[] Buttons(Item it, int rowTop)
        {
            Act[] acts = Actions(it);
            int s = BtnS, gap = (int)(4 * Dpi96), pad = (int)(10 * Dpi96);
            Rectangle[] r = new Rectangle[acts.Length];
            int x = Width - pad - s;
            for (int i = acts.Length - 1; i >= 0; i--, x -= s + gap)
                r[i] = new Rectangle(x, rowTop + (int)(36 * Dpi96), s, s);
            return r;
        }

        bool Hit(Point p, out Item item, out Act act)
        {
            item = ItemAt(p); act = Act.Delete;
            if (item == null) return false;
            int top = (p.Y + scroll) / RowH * RowH - scroll;
            Act[] acts = Actions(item);
            Rectangle[] rs = Buttons(item, top);
            for (int i = 0; i < rs.Length; i++)
                if (rs[i].Contains(p)) { act = acts[i]; return true; }
            return false;
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int max = Math.Max(0, view.Length * RowH - Height);
            scroll = Math.Max(0, Math.Min(max, scroll - e.Delta / 2));
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            Item it; Act a;
            bool on = Hit(e.Location, out it, out a);
            Item hi = on ? it : null; Act? ha = on ? (Act?)a : null;
            int row = (e.Y + scroll) / RowH;
            if (row != hot || hi != hotItem || ha != hotAct)
            {
                hot = row; hotItem = hi; hotAct = ha;
                Cursor = on ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hot = -1; hotItem = null; hotAct = null; Cursor = Cursors.Default; Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Item it; Act a;
            if (e.Button == MouseButtons.Left && Hit(e.Location, out it, out a) && Command != null)
                Command(it, a);
            base.OnMouseDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Draw.Setup(g);
            g.Clear(Theme.Bg);
            if (view.Length == 0)
            {
                using (SolidBrush b = new SolidBrush(Theme.Dim))
                    g.DrawString("Скопируйте ссылку на YouTube и нажмите горячую клавишу",
                        font, b, new RectangleF(16, 20, Width - 32, 40));
                return;
            }
            int reserve = (BtnS + (int)(4 * Dpi96)) * 4 + (int)(6 * Dpi96);
            for (int i = 0; i < view.Length; i++)
            {
                int y = i * RowH - scroll;
                if (y + RowH < 0 || y > Height) continue;
                Rectangle r = new Rectangle(0, y, Width, RowH);
                if (i == hot) using (SolidBrush b = new SolidBrush(Theme.Panel)) g.FillRectangle(b, r);
                Rectangle body = r; body.Y += (int)(8 * Dpi96);
                Draw.Row(g, view[i], body, Dpi96, font, small, reserve, true);
                Act[] acts = Actions(view[i]);
                Rectangle[] rs = Buttons(view[i], y);
                for (int k = 0; k < rs.Length; k++)
                    Glyph(g, acts[k], rs[k], hotItem == view[i] && hotAct == acts[k]);
                using (Pen p = new Pen(Theme.Line)) g.DrawLine(p, 0, y + RowH - 1, Width, y + RowH - 1);
            }
        }

        // Значки рисуем фигурами — без шрифтов с иконками и без картинок.
        static void Glyph(Graphics g, Act a, Rectangle r, bool hover)
        {
            Color c = hover ? (a == Act.Delete ? Theme.Err : Theme.Text) : Theme.Dim;
            if (hover) using (SolidBrush bg = new SolidBrush(Theme.Hover)) g.FillRectangle(bg, r);
            int m = r.Width / 4;                    // внутренний отступ
            Rectangle i = Rectangle.Inflate(r, -m, -m);
            using (SolidBrush b = new SolidBrush(c))
            using (Pen p = new Pen(c, Math.Max(1.5f, r.Width / 12f)))
            {
                switch (a)
                {
                    case Act.Pause:
                        int bw = Math.Max(2, i.Width / 3);
                        g.FillRectangle(b, i.X, i.Y, bw, i.Height);
                        g.FillRectangle(b, i.Right - bw, i.Y, bw, i.Height);
                        break;
                    case Act.Resume:
                        g.FillPolygon(b, new[] { new Point(i.X + 1, i.Y), new Point(i.Right, i.Y + i.Height / 2), new Point(i.X + 1, i.Bottom) });
                        break;
                    case Act.Folder:
                        g.FillRectangle(b, i.X, i.Y, i.Width * 2 / 5, Math.Max(2, i.Height / 5));
                        g.FillRectangle(b, i.X, i.Y + i.Height / 5, i.Width, i.Height * 4 / 5 - 1);
                        break;
                    case Act.Stop:
                        g.FillRectangle(b, i);
                        break;
                    default:
                        g.DrawLine(p, i.X, i.Y, i.Right, i.Bottom);
                        g.DrawLine(p, i.Right, i.Y, i.X, i.Bottom);
                        break;
                }
            }
        }
    }
}
