using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace OpenD
{
    public enum Act { Folder, Pause, Resume, Stop, Delete }

    // Список загрузок, рисуется вручную. Справа четыре фиксированных слота для значков:
    // папка · пауза/продолжить · стоп · убрать. Слоты не меняют места при смене состояния.
    class QueueList : Control
    {
        readonly Engine eng;
        int scroll, hot = -1, hotSlot = -1, lastCheck;
        Item[] view = new Item[0];
        readonly Font hintFont = Fonts.Body(10f);

        public event Action<Item, Act> Command;

        public QueueList(Engine e)
        {
            eng = e;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
        }

        float K { get { return DeviceDpi / 96f; } }
        int RowH { get { return (int)(Draw.RowUnits * K); } }

        public void Refresh2()
        {
            view = eng.Snapshot().OrderByDescending(i => i.Added).ToArray();
            if (Environment.TickCount - lastCheck > 2000)           // удалили файл через проводник — отметить в списке
            {
                lastCheck = Environment.TickCount;
                foreach (Item it in view)
                    if (it.State == St.Done && !string.IsNullOrEmpty(it.File)) it.Missing = !File.Exists(it.File);
            }
            int max = Math.Max(0, view.Length * RowH - Height);
            if (scroll > max) scroll = max;
            Invalidate();
        }

        public Item ItemAt(Point p)
        {
            int i = (p.Y + scroll) / RowH;
            return i >= 0 && p.Y + scroll >= 0 && i < view.Length ? view[i] : null;
        }

        // Действие в слоте 0..3 для состояния строки (null — слот пуст).
        static Act? SlotAct(Item it, int slot)
        {
            bool live = it.State == St.Active || it.State == St.Queued;
            switch (slot)
            {
                case 0: return Act.Folder;
                case 1:
                    if (live) return Act.Pause;
                    if (it.State == St.Done) return it.Missing ? (Act?)Act.Resume : null;
                    return Act.Resume;
                case 2: return live || it.State == St.Paused ? (Act?)Act.Stop : null;
                default: return Act.Delete;
            }
        }

        Rectangle SlotRect(int slot, int rowTop)
        {
            int s = Draw.Slot(K), right = Width - (int)(12 * K);
            return new Rectangle(right - (4 - slot) * s, rowTop + (RowH - s) / 2, s, s);
        }

        bool Hit(Point p, out Item item, out Act act, out int slot)
        {
            item = ItemAt(p); act = Act.Delete; slot = -1;
            if (item == null) return false;
            int top = (p.Y + scroll) / RowH * RowH - scroll;
            for (int i = 0; i < 4; i++)
            {
                Act? a = SlotAct(item, i);
                if (a != null && Rectangle.Inflate(SlotRect(i, top), 1, 9).Contains(p)) { act = a.Value; slot = i; return true; }
            }
            return false;
        }

        static string Tip(Act a, Item it)
        {
            switch (a)
            {
                case Act.Folder: return "Показать файл в папке";
                case Act.Pause: return "Пауза";
                case Act.Resume: return it.State == St.Done ? "Скачать заново" : "Продолжить";
                case Act.Stop: return "Стоп (удалить недокачанное)";
                default: return "Убрать из списка";
            }
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int max = Math.Max(0, view.Length * RowH - Height);
            scroll = Math.Max(0, Math.Min(max, scroll - e.Delta / 2));
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            Item it; Act a; int slot;
            bool on = Hit(e.Location, out it, out a, out slot);
            int row = (e.Y + scroll) / RowH;
            if (row == hot && slot == hotSlot) return;
            hot = row; hotSlot = slot;
            Cursor = on ? Cursors.Hand : Cursors.Default;
            Invalidate();
            if (on) Ui.Hint(this, Tip(a, it), new Point(e.X - 20, e.Y + 22));
            else Ui.Hint(this, null, Point.Empty);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hot = -1; hotSlot = -1; Cursor = Cursors.Default; Invalidate(); Ui.Hint(this, null, Point.Empty);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Item it; Act a; int slot;
            if (e.Button == MouseButtons.Left && Hit(e.Location, out it, out a, out slot) && Command != null)
            {
                Ui.Hint(this, null, Point.Empty);
                Command(it, a);
            }
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
                        hintFont, b, new RectangleF(20, 24, Width - 40, 60));
                return;
            }
            for (int i = 0; i < view.Length; i++)
            {
                int y = i * RowH - scroll;
                if (y + RowH < 0 || y > Height) continue;
                Rectangle r = new Rectangle(0, y, Width, RowH);
                if (i == hot) using (SolidBrush b = new SolidBrush(Theme.Panel)) g.FillRectangle(b, r);
                Draw.Row(g, view[i], r, K);
                for (int sl = 0; sl < 4; sl++)
                {
                    Act? a = SlotAct(view[i], sl);
                    if (a != null) Glyph(g, a.Value, SlotRect(sl, y), i == hot && sl == hotSlot);
                }
                using (Pen p = new Pen(Theme.Line)) g.DrawLine(p, 0, y + RowH - 1, Width, y + RowH - 1);
            }
        }

        void Glyph(Graphics g, Act a, Rectangle r, bool hover)
        {
            Color c = hover ? (a == Act.Delete ? Theme.Err : Theme.Text) : Theme.Dim;
            if (hover)
                using (GraphicsPath gp = Theme.Round(r, (int)(5 * K)))
                using (SolidBrush bg = new SolidBrush(Theme.Hover)) g.FillPath(bg, gp);
            Ic ic = a == Act.Folder ? Ic.Folder : a == Act.Pause ? Ic.Pause : a == Act.Resume ? Ic.Play : a == Act.Stop ? Ic.Stop : Ic.Close;
            Icons.Draw(g, ic, r, c, a == Act.Delete ? 33 : 24);       // крестик мельче остальных значков
        }
    }
}
