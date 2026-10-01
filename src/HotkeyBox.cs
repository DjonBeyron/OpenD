using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace OpenD
{
    // Поле «нажмите сочетание»: клик, затем любое сочетание с Ctrl/Alt/Shift/Win. Esc — отмена.
    class HotkeyBox : Control
    {
        uint mods;
        int key;
        bool capturing;
        string note;
        readonly Func<uint, int, bool> apply;      // true — сочетание принято (зарегистрировано)
        readonly Timer noteTimer = new Timer { Interval = 1800 };
        readonly Font font = Fonts.Body(9.5f);

        public HotkeyBox(uint m, int k, Func<uint, int, bool> applyHotkey)
        {
            mods = m; key = k; apply = applyHotkey;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.Selectable | ControlStyles.StandardClick, true);
            TabStop = true;
            Cursor = Cursors.Hand;
            Size = new Size(230, 32);
            noteTimer.Tick += (s, e) => { noteTimer.Stop(); note = null; Invalidate(); };
        }

        protected override void OnClick(EventArgs e) { capturing = true; note = null; Focus(); Invalidate(); base.OnClick(e); }
        protected override void OnLostFocus(EventArgs e) { capturing = false; Invalidate(); base.OnLostFocus(e); }
        protected override bool IsInputKey(Keys keyData) { return true; }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (!capturing) return base.ProcessCmdKey(ref msg, keyData);
            Keys code = keyData & Keys.KeyCode;
            if (code == Keys.Escape) { capturing = false; Invalidate(); return true; }
            if (code == Keys.ControlKey || code == Keys.ShiftKey || code == Keys.Menu ||
                code == Keys.LWin || code == Keys.RWin) return true;           // ждём основную клавишу
            uint m = 0;
            if ((keyData & Keys.Alt) != 0) m |= 1;
            if ((keyData & Keys.Control) != 0) m |= 2;
            if ((keyData & Keys.Shift) != 0) m |= 4;
            if ((Native.GetAsyncKeyState(0x5B) & 0x8000) != 0 || (Native.GetAsyncKeyState(0x5C) & 0x8000) != 0) m |= 8;
            capturing = false;
            if (m == 0) Flash("нужен Ctrl/Alt/Shift/Win");
            else if (apply(m, (int)code)) { mods = m; key = (int)code; }
            else Flash("занято, выберите другое");
            Invalidate();
            return true;
        }

        void Flash(string text) { note = text; noteTimer.Stop(); noteTimer.Start(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Draw.Setup(g);
            g.Clear(Theme.Bg);
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath gp = Theme.Round(r, 6))
            using (SolidBrush bg = new SolidBrush(Theme.Panel))
            using (Pen pen = new Pen(capturing ? Theme.Accent : note != null ? Theme.Err : Theme.Line))
            {
                g.FillPath(bg, gp);
                g.DrawPath(pen, gp);
            }
            string text = capturing ? "нажмите сочетание…" : note ?? Settings.HotkeyText(mods, key);
            Color c = capturing ? Theme.Accent : note != null ? Theme.Err : Theme.Text;
            using (SolidBrush tb = new SolidBrush(c))
            using (StringFormat sf = new StringFormat { LineAlignment = StringAlignment.Center, Alignment = StringAlignment.Center })
                g.DrawString(text, font, tb, new RectangleF(0, 0, Width, Height), sf);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { noteTimer.Dispose(); font.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
