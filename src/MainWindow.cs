using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
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
            b.Text = text;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderColor = Theme.Line;
            b.FlatAppearance.MouseOverBackColor = Theme.Hover;
            b.FlatAppearance.MouseDownBackColor = Theme.Line;
            b.BackColor = Theme.Panel;
            b.ForeColor = Theme.Text;
            b.Font = new Font("Segoe UI", 9f);
            b.AutoSize = true;
            b.Padding = new Padding(6, 2, 6, 2);
            b.Cursor = Cursors.Hand;
            b.Click += click;
            return b;
        }
    }

    class MainWindow : Form
    {
        readonly Engine eng;
        readonly Settings cfg;
        readonly QueueList list;
        readonly Label foot = new Label();
        readonly Timer tick = new Timer { Interval = 400 };
        readonly Action grab;
        public bool Quitting;

        Button qBtn, cBtn;

        public MainWindow(Engine e, Settings s, Action grabClipboard, Action openSettings)
        {
            eng = e; cfg = s; grab = grabClipboard;
            Text = "OpenD";
            Icon = Native.MakeIcon();
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9f);
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(660, 600);
            MinimumSize = new Size(640, 360);
            KeyPreview = true;

            FlowLayoutPanel top = new FlowLayoutPanel
            {
                Dock = DockStyle.Top, Height = 48, BackColor = Theme.Bg,
                Padding = new Padding(10, 8, 0, 0)
            };
            top.Controls.Add(Ui.Btn("+ Из буфера", (a, b) => grab()));
            qBtn = Ui.Btn("", (a, b) => Pick(qBtn, Ytdlp.Qualities, () => cfg.Quality, k => cfg.Quality = k));
            cBtn = Ui.Btn("", (a, b) => Pick(cBtn, Ytdlp.Containers, () => cfg.Container, k => cfg.Container = k));
            top.Controls.Add(qBtn);
            top.Controls.Add(cBtn);
            top.Controls.Add(Ui.Btn("Папка", (a, b) => OpenFolder()));
            top.Controls.Add(Ui.Btn("Настройки", (a, b) => openSettings()));
            RefreshHeader();
            foot.Dock = DockStyle.Bottom;
            foot.Height = 28;
            foot.ForeColor = Theme.Dim;
            foot.TextAlign = ContentAlignment.MiddleLeft;
            foot.Padding = new Padding(12, 0, 0, 0);
            list = new QueueList(e) { Dock = DockStyle.Fill };

            ContextMenuStrip cm = Ui.Menu();
            Ui.Add(cm, "Открыть файл", (a, b) => Do(it => Open(it.File)));
            Ui.Add(cm, "Показать в папке", (a, b) => Do(Reveal));
            Ui.Add(cm, "Продолжить / повторить", (a, b) => Do(eng.Resume));
            Ui.Add(cm, "Показать ошибку", (a, b) => Do(it => MessageBox.Show(string.IsNullOrEmpty(it.Error) ? "Ошибок нет." : it.Error, "OpenD")));
            Ui.Add(cm, "Копировать ссылку", (a, b) => Do(it => Clipboard.SetText(it.Url)));
            Ui.Add(cm, "Убрать из списка", (a, b) => Do(it => eng.Remove(it)));
            Ui.Add(cm, "Удалить вместе с файлом", (a, b) => Do(DeleteWithFile));
            list.Command += (it, act) =>
            {
                if (act == Act.Folder) Reveal(it);
                else if (act == Act.Pause) eng.Pause(it);
                else if (act == Act.Resume) eng.Resume(it);
                else if (act == Act.Stop) eng.Stop(it);
                else eng.Remove(it);
                list.Refresh2();
            };
            list.MouseUp += (a, m) =>
            {
                if (m.Button == MouseButtons.Right && list.ItemAt(m.Location) != null)
                    cm.Show(list, m.Location);
            };
            list.MouseDoubleClick += (a, m) =>
            {
                Item it = list.ItemAt(m.Location);
                if (it != null && it.State == St.Done) Open(it.File);
            };
            cm.Opening += (a, c) => { Item it = Selected(); if (it == null) c.Cancel = true; };

            Controls.Add(list);
            Controls.Add(foot);
            Controls.Add(top);

            KeyDown += (a, k) =>
            {
                if (k.Control && k.KeyCode == Keys.V) { grab(); k.Handled = true; }
            };
            tick.Tick += (a, b) => { if (Visible) Update2(); };
            tick.Start();
        }

        Point lastMenu;
        Item Selected()
        {
            lastMenu = list.PointToClient(Cursor.Position);
            return list.ItemAt(lastMenu);
        }

        void Do(Action<Item> f)
        {
            Item it = list.ItemAt(lastMenu);
            if (it != null) f(it);
        }

        void Update2()
        {
            list.Refresh2();
            int act = eng.Snapshot().Count(i => i.State == St.Active || i.State == St.Queued);
            foot.Text = eng.Note ?? ((act > 0 ? "Идёт: " + act + "   ·   " : "") + cfg.Folder);
        }

        public void ShowFront()
        {
            Update2();
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        void OpenFolder()
        {
            Directory.CreateDirectory(cfg.Folder);
            Process.Start("explorer.exe", "\"" + cfg.Folder + "\"");
        }

        static void Open(string file)
        {
            if (!string.IsNullOrEmpty(file) && File.Exists(file)) Process.Start(file);
            else MessageBox.Show("Файл не найден (возможно, удалён).", "OpenD");
        }

        void DeleteWithFile(Item it)
        {
            if (MessageBox.Show("Удалить из списка и стереть файл с диска?", "OpenD", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                eng.Remove(it, true);
        }

        // Папка именно этого файла (с выделением); если файла ещё нет — общая папка загрузок.
        void Reveal(Item it)
        {
            if (!string.IsNullOrEmpty(it.File) && File.Exists(it.File))
                Process.Start("explorer.exe", "/select,\"" + it.File + "\"");
            else OpenFolder();
        }

        public void RefreshHeader()
        {
            qBtn.Text = "Качество: " + Presets.Short(Ytdlp.QualityName(cfg.Quality));
            cBtn.Text = "Формат: " + Presets.Short(Ytdlp.ContainerName(cfg.Container));
        }

        void Pick(Control anchor, string[][] opts, Func<string> cur, Action<string> set)
        {
            ContextMenuStrip m = Ui.Menu();
            Presets.Fill(m.Items, opts, cur(), k => { set(k); Store.SaveSettings(cfg); RefreshHeader(); });
            m.Show(anchor, new Point(0, anchor.Height));
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.Style(Handle, false);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!Quitting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
            base.OnFormClosing(e);
        }
    }
}

