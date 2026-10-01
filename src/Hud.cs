using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace OpenD
{
    // Мини-окно в правом нижнем углу: только название и процент. Не забирает фокус.
    // Появляется при загрузках (если включено), по горячей клавише показывается/скрывается вручную.
    class Hud : Form
    {
        const int MaxRows = 3;

        readonly Engine eng;
        readonly Settings cfg;
        readonly Timer tick = new Timer { Interval = 250 };
        readonly Font fTitle = Fonts.Title(8.5f), fBody = Fonts.Body(8f), fBold = Fonts.BodyBold(8.5f);
        readonly StringFormat ell = new StringFormat(StringFormatFlags.NoWrap) { Trimming = StringTrimming.EllipsisCharacter };
        string msg;
        bool pinned;                               // показано вручную — не скрывать само
        DateTime msgUntil = DateTime.MinValue, hideAt = DateTime.MinValue;
        List<Item> rows = new List<Item>();

        public Hud(Engine e, Settings s)
        {
            eng = e; cfg = s;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            BackColor = Theme.Bg;
            DoubleBuffered = true;
            Width = Px(236);
            Height = Px(40);
            Click += (a, b) => { pinned = false; Hide(); };
            tick.Tick += (a, b) => Step();
            tick.Start();
        }

        float K { get { return DeviceDpi / 96f; } }
        int Px(float v) { return (int)(v * K); }

        public bool IsShown { get { return Visible; } }

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

        // Показать на несколько секунд (и держать, пока идут загрузки). Если мини-окно отключено — молчит.
        public void Pop(string message)
        {
            if (!cfg.HudAuto && !pinned) return;
            if (message != null) { msg = message; msgUntil = DateTime.UtcNow.AddSeconds(4); }
            hideAt = DateTime.UtcNow.AddSeconds(4);
            Present();
        }

        // Горячая клавиша / значок: показать (закреплено) или скрыть.
        public void Toggle()
        {
            if (Visible) { pinned = false; Hide(); return; }
            pinned = true;
            Present();
        }

        void Present()
        {
            Relayout();
            if (!Visible) Show();
            Invalidate();
        }

        void Step()
        {
            if (!Visible) return;
            DateTime now = DateTime.UtcNow;
            if (eng.Pending() > 0 || eng.Note != null) hideAt = now.AddSeconds(3);
            if (!pinned && now > hideAt) { Hide(); return; }
            Relayout();
            Invalidate();
        }

        string Head()
        {
            if (eng.Note != null) return eng.Note;
            if (msg != null && DateTime.UtcNow < msgUntil) return msg;
            return null;
        }

        void Relayout()
        {
            DateTime now = DateTime.UtcNow;
            Item[] all = eng.Snapshot();
            rows = all.Where(i => i.State == St.Active)
                .Concat(all.Where(i => i.State == St.Queued))
                .Concat(all.Where(i => i.State == St.Done || i.State == St.Failed).OrderByDescending(i => i.Added).Take(1))
                .Take(MaxRows).ToList();
            if (rows.Count > 0 && rows.All(i => i.State == St.Done || i.State == St.Failed) && now > msgUntil && !pinned)
                rows.Clear();

            string head = Head();
            int h = Px(10);
            if (head != null) h += Px(18);
            h += rows.Count * Px(26);
            if (rows.Count == 0 && head == null) h += Px(18);
            h += Px(2);
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            int m = Px(14);
            Rectangle target = new Rectangle(wa.Right - Width - m, wa.Bottom - h - m, Width, h);
            if (Bounds != target) Bounds = target;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Draw.Setup(g);
            g.Clear(Theme.Bg);
            using (Pen pen = new Pen(Theme.Line)) g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
            int pad = Px(12), w = Width - pad * 2, y = Px(8);
            string head = Head();
            if (head != null)
            {
                using (SolidBrush b = new SolidBrush(Theme.Text))
                    g.DrawString(head, fBody, b, new RectangleF(pad, y, w, Px(16)), ell);
                y += Px(18);
            }
            else if (rows.Count == 0)
                using (SolidBrush b = new SolidBrush(Theme.Dim))
                    g.DrawString("Очередь пуста", fBody, b, pad, y);
            foreach (Item it in rows) { Row(g, it, pad, y, w); y += Px(26); }
        }

        void Row(Graphics g, Item it, int x, int y, int w)
        {
            string right; Color rc = Theme.Dim, tc = Theme.Text;
            bool stalled = it.State == St.Active && it.Got > 0 && Environment.TickCount - it.LastTick > 6000;
            switch (it.State)
            {
                case St.Active:
                    right = it.Got <= 0 ? "…" : stalled ? "нет сети" : (int)it.Percent + "%";
                    rc = stalled ? Theme.Warn : Theme.Accent; break;
                case St.Done: right = "готово"; rc = Theme.Ok; break;
                case St.Failed: right = "ошибка"; rc = Theme.Err; tc = Theme.Dim; break;
                default:
                    right = it.RetryAt > DateTime.UtcNow.Ticks ? "нет сети" : "очередь";
                    rc = it.RetryAt > DateTime.UtcNow.Ticks ? Theme.Warn : Theme.Dim; tc = Theme.Dim; break;
            }
            int rw = Px(64);                                   // фиксированная колонка справа — текст не «пляшет»
            string title = string.IsNullOrEmpty(it.Title) ? it.Url : it.Title;
            using (SolidBrush tb = new SolidBrush(tc))
                g.DrawString(title, fTitle, tb, new RectangleF(x, y, w - rw - Px(4), Px(16)), ell);
            using (SolidBrush rb = new SolidBrush(rc))
            using (StringFormat far = new StringFormat(StringFormatFlags.NoWrap) { Alignment = StringAlignment.Far })
                g.DrawString(right, fBold, rb, new RectangleF(x + w - rw, y, rw, Px(16)), far);
            int by = y + Px(18), bh = Math.Max(2, Px(2));
            using (SolidBrush bg = new SolidBrush(Theme.Line)) g.FillRectangle(bg, x, by, w, bh);
            double p = it.State == St.Done ? 100 : it.Percent;
            if (p > 0)
                using (SolidBrush fb = new SolidBrush(it.State == St.Done ? Theme.Ok : Theme.Accent))
                    g.FillRectangle(fb, x, by, (int)(w * Math.Min(100, p) / 100.0), bh);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { tick.Dispose(); fTitle.Dispose(); fBody.Dispose(); fBold.Dispose(); ell.Dispose(); }
            base.Dispose(disposing);
        }
    }
}

