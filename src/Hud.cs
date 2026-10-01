using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace OpenD
{
    // Мини-HUD в правом нижнем углу: не забирает фокус, держится пока идут загрузки.
    class Hud : Form
    {
        const int MaxRows = 3;

        readonly Engine eng;
        readonly Timer tick = new Timer { Interval = 250 };
        readonly Font font, small;
        string msg;
        DateTime msgUntil = DateTime.MinValue, hideAt = DateTime.MinValue;
        List<Item> rows = new List<Item>();

        public Hud(Engine e)
        {
            eng = e;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            BackColor = Theme.Bg;
            DoubleBuffered = true;
            font = new Font("Segoe UI", 9f);
            small = new Font("Segoe UI", 8f);
            Width = Px(360);
            Height = Px(60);
            Click += (s, a) => Hide();
            tick.Tick += (s, a) => Step();
            tick.Start();
        }

        float Dpi96 { get { return DeviceDpi / 96f; } }
        int Px(int v) { return (int)(v * Dpi96); }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000 | 0x80 | 0x8;   // NOACTIVATE | TOOLWINDOW | TOPMOST
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.Style(Handle, true);
        }

        // Показать HUD на несколько секунд (и держать, пока идут загрузки).
        public void Pop(string message)
        {
            if (message != null) { msg = message; msgUntil = DateTime.UtcNow.AddSeconds(4); }
            hideAt = DateTime.UtcNow.AddSeconds(4);
            Layout2();
            if (!Visible) Show();
            Invalidate();
        }

        void Step()
        {
            if (!Visible) return;
            DateTime now = DateTime.UtcNow;
            if (eng.Pending() > 0 || eng.Note != null) hideAt = now.AddSeconds(3);
            if (now > hideAt) { Hide(); return; }
            Layout2();
            Invalidate();
        }

        void Layout2()
        {
            DateTime now = DateTime.UtcNow;
            long recent = now.AddSeconds(-8).Ticks;
            Item[] all = eng.Snapshot();
            rows = all.Where(i => i.State == St.Active)
                .Concat(all.Where(i => i.State == St.Queued))
                .Concat(all.Where(i => (i.State == St.Done || i.State == St.Failed) && i.Added > 0)
                    .OrderByDescending(i => i.Added).Take(1))
                .Take(MaxRows).ToList();
            // завершённые показываем только пока HUD «живой» из-за недавнего события
            if (rows.Count > 0 && rows.All(i => i.State == St.Done || i.State == St.Failed) && now > msgUntil && eng.Note == null)
                rows.Clear();

            int h = Px(14);
            if (HeadText() != null) h += Px(26);
            h += rows.Count * Px(52);
            if (rows.Count == 0 && HeadText() == null) h += Px(26);
            if (rows.Count > 0 && HeadText() == null) h += 0;
            h += Px(4);
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            int m = Px(16);
            Rectangle target = new Rectangle(wa.Right - Width - m, wa.Bottom - h - m, Width, h);
            if (Bounds != target) Bounds = target;
        }

        string HeadText()
        {
            if (eng.Note != null) return eng.Note;
            if (msg != null && DateTime.UtcNow < msgUntil) return msg;
            return null;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Draw.Setup(g);
            g.Clear(Theme.Bg);
            using (Pen pen = new Pen(Theme.Line))
                g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            int y = Px(10);
            int pad = Px(14);
            string head = HeadText();
            using (SolidBrush tb = new SolidBrush(Theme.Text))
            using (SolidBrush db = new SolidBrush(Theme.Dim))
            {
                if (head != null)
                {
                    g.DrawString(head, font, tb, new RectangleF(pad, y, Width - pad * 2, Px(22)),
                        new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter });
                    y += Px(26);
                }
                else if (rows.Count == 0)
                    g.DrawString("Очередь пуста", font, db, pad, y);
            }
            foreach (Item it in rows)
            {
                Draw.Row(g, it, new Rectangle(0, y, Width, Px(52)), Dpi96, font, small);
                y += Px(52);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { tick.Dispose(); font.Dispose(); small.Dispose(); }
            base.Dispose(disposing);
        }
    }
}

