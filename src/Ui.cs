using System;
using System.Drawing;
using System.Windows.Forms;

namespace OpenD
{
    class DarkColors : ProfessionalColorTable
    {
        public override Color MenuItemSelected { get { return Theme.Hover; } }
        public override Color MenuItemBorder { get { return Theme.Hover; } }
        public override Color ToolStripDropDownBackground { get { return Theme.Panel; } }
        public override Color ImageMarginGradientBegin { get { return Theme.Panel; } }
        public override Color ImageMarginGradientMiddle { get { return Theme.Panel; } }
        public override Color ImageMarginGradientEnd { get { return Theme.Panel; } }
        public override Color MenuBorder { get { return Theme.Line; } }
        public override Color SeparatorDark { get { return Theme.Line; } }
        public override Color SeparatorLight { get { return Theme.Line; } }
    }

    static class Ui
    {
        static ToolTip tip;

        // Меню рисует система (GDI), поэтому здесь Segoe UI; остальной интерфейс — Montserrat/Comfortaa.
        public static ContextMenuStrip Menu()
        {
            ContextMenuStrip m = new ContextMenuStrip();
            m.Renderer = new ToolStripProfessionalRenderer(new DarkColors());
            m.BackColor = Theme.Panel;
            m.ForeColor = Theme.Text;
            m.Font = new Font("Segoe UI", 9f);
            m.ShowImageMargin = false;
            return m;
        }

        public static ToolStripMenuItem Add(ContextMenuStrip m, string text, EventHandler click)
        {
            ToolStripMenuItem i = new ToolStripMenuItem(text);
            i.ForeColor = Theme.Text;
            if (click != null) i.Click += click;
            m.Items.Add(i);
            return i;
        }

        public static Button Btn(string text, EventHandler click)
        {
            Button b = new Button();
            b.UseCompatibleTextRendering = true;       // GDI+ умеет шрифты из ресурсов
            b.Text = text;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = Theme.Line;
            b.FlatAppearance.MouseOverBackColor = Theme.Hover;
            b.FlatAppearance.MouseDownBackColor = Theme.Line;
            b.BackColor = Theme.Panel;
            b.ForeColor = Theme.Text;
            b.Font = Fonts.Body(9f);
            b.AutoSize = true;
            b.Padding = new Padding(8, 3, 8, 3);
            b.Cursor = Cursors.Hand;
            b.Click += click;
            return b;
        }

        public static Label Text(string text, bool dim, float pt = 9.5f)
        {
            return new Label
            {
                UseCompatibleTextRendering = true, AutoSize = true, Text = text, Font = Fonts.Body(pt),
                ForeColor = dim ? Theme.Dim : Theme.Text, BackColor = Color.Transparent, Margin = new Padding(3, 4, 3, 4)
            };
        }

        // Тёмная подсказка для самодельных контролов: Hint(control, "текст", точка) / Hint(control, null, …) — скрыть.
        public static void Hint(Control c, string text, Point at)
        {
            if (tip == null)
            {
                tip = new ToolTip { OwnerDraw = true, ShowAlways = true, InitialDelay = 0 };
                Font f = Fonts.Body(8.5f);
                tip.Popup += (s, e) =>
                {
                    using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
                    {
                        SizeF m = g.MeasureString(tip.GetToolTip(e.AssociatedControl), f);
                        e.ToolTipSize = new Size((int)m.Width + 14, (int)m.Height + 10);
                    }
                };
                tip.Draw += (s, e) =>
                {
                    using (SolidBrush bg = new SolidBrush(Theme.Panel)) e.Graphics.FillRectangle(bg, e.Bounds);
                    using (Pen pen = new Pen(Theme.Line)) e.Graphics.DrawRectangle(pen, 0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);
                    Draw.Setup(e.Graphics);
                    using (SolidBrush tb = new SolidBrush(Theme.Text))
                        e.Graphics.DrawString(e.ToolTipText, f, tb, 7, 5);
                };
            }
            if (text == null) { tip.Hide(c); return; }
            tip.Show(text, c, at.X, at.Y, 4000);
        }
    }
}
