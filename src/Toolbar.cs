using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace OpenD
{
    // Верхняя панель: значки и «таблетки» качества/формата, подсветка при наведении, тёмные подсказки.
    class Toolbar : Control
    {
        class Btn
        {
            public Ic Icon; public Func<string> Label; public string Tip; public Rectangle Rect;
            public Action<Point> Click; public bool Right; public Func<Ic> Dynamic;
        }

        readonly List<Btn> items = new List<Btn>();
        readonly Font font = Fonts.Body(9f);
        int hot = -1;

        public event Action Add, ToggleHud, OpenFolder, OpenSettings;
        public event Action<Point> PickQuality, PickContainer;
        public Func<string> QualityText, ContainerText;
        public Func<bool> HudShown;

        public Toolbar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Dock = DockStyle.Top;
            BackColor = Theme.Bg;
            Height = 46;
        }

        public void Build()
        {
            items.Clear();
            items.Add(new Btn { Icon = Ic.Plus, Tip = "Добавить ссылку из буфера (Ctrl+V)", Click = p => { if (Add != null) Add(); } });
            items.Add(new Btn { Icon = Ic.Chevron, Label = QualityText, Tip = "Качество видео", Click = p => { if (PickQuality != null) PickQuality(p); } });
            items.Add(new Btn { Icon = Ic.Chevron, Label = ContainerText, Tip = "Формат файла (контейнер)", Click = p => { if (PickContainer != null) PickContainer(p); } });
            items.Add(new Btn { Right = true, Icon = Ic.Gear, Tip = "Настройки", Click = p => { if (OpenSettings != null) OpenSettings(); } });
            items.Add(new Btn { Right = true, Icon = Ic.Folder, Tip = "Папка загрузок", Click = p => { if (OpenFolder != null) OpenFolder(); } });
            items.Add(new Btn
            {
                Right = true, Icon = Ic.Eye, Tip = "Показать / скрыть мини-окно",
                Dynamic = () => HudShown != null && HudShown() ? Ic.Eye : Ic.EyeOff,
                Click = p => { if (ToggleHud != null) ToggleHud(); }
            });
            Invalidate();
        }

        float K { get { return DeviceDpi / 96f; } }

        void Layout2(Graphics g)
        {
            int slot = (int)(32 * K), gap = (int)(4 * K), pad = (int)(12 * K), y = (Height - slot) / 2;
            int xl = pad, xr = Width - pad;
            foreach (Btn b in items)
            {
                int w = slot;
                if (b.Label != null) w = (int)g.MeasureString(b.Label(), font).Width + (int)(30 * K);
                if (b.Right) { xr -= w; b.Rect = new Rectangle(xr, y, w, slot); xr -= gap; }
                else { b.Rect = new Rectangle(xl, y, w, slot); xl += w + gap; }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Draw.Setup(g);
            g.Clear(Theme.Bg);
            Layout2(g);
            for (int i = 0; i < items.Count; i++)
            {
                Btn b = items[i];
                bool on = i == hot;
                if (on)
                    using (GraphicsPath gp = Theme.Round(b.Rect, (int)(6 * K)))
                    using (SolidBrush hb = new SolidBrush(Theme.Hover)) g.FillPath(hb, gp);
                Color c = on ? Theme.Text : Theme.Dim;
                if (b.Label != null)
                {
                    using (SolidBrush tb = new SolidBrush(c))
                    using (StringFormat sf = new StringFormat { LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
                        g.DrawString(b.Label(), font, tb, new RectangleF(b.Rect.X + 10 * K, b.Rect.Y, b.Rect.Width - 20 * K, b.Rect.Height), sf);
                    Icons.Draw(g, Ic.Chevron, new Rectangle(b.Rect.Right - (int)(22 * K), b.Rect.Y, (int)(20 * K), b.Rect.Height), c);
                }
                else Icons.Draw(g, b.Dynamic != null ? b.Dynamic() : b.Icon, b.Rect, c);
            }
            using (Pen p = new Pen(Theme.Line)) g.DrawLine(p, 0, Height - 1, Width, Height - 1);
        }

        int Find(Point p)
        {
            for (int i = 0; i < items.Count; i++) if (items[i].Rect.Contains(p)) return i;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int i = Find(e.Location);
            if (i == hot) return;
            hot = i;
            Cursor = i >= 0 ? Cursors.Hand : Cursors.Default;
            Invalidate();
            if (i >= 0) Ui.Hint(this, items[i].Tip, new Point(items[i].Rect.X, items[i].Rect.Bottom + 4));
            else Ui.Hint(this, null, Point.Empty);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hot = -1; Cursor = Cursors.Default; Invalidate(); Ui.Hint(this, null, Point.Empty);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            int i = Find(e.Location);
            if (e.Button != MouseButtons.Left || i < 0) return;
            Ui.Hint(this, null, Point.Empty);
            items[i].Click(new Point(items[i].Rect.X, items[i].Rect.Bottom));
        }
    }
}
