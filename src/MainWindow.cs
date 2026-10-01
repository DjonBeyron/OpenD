using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace OpenD
{
    class MainWindow : Form
    {
        readonly Engine eng;
        readonly Settings cfg;
        readonly QueueList list;
        readonly Label foot = new Label();
        readonly Timer tick = new Timer { Interval = 400 };
        readonly Action grab;
        public bool Quitting;

        readonly Toolbar bar = new Toolbar();

        public MainWindow(Engine e, Settings s, Action grabClipboard, Action openSettings, Action toggleHud, Func<bool> hudShown)
        {
            eng = e; cfg = s; grab = grabClipboard;
            Text = "OpenD";
            Icon = Native.MakeIcon();
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Fonts.Body(9f);
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(700, 600);
            MinimumSize = new Size(640, 360);
            KeyPreview = true;

            bar.QualityText = () => Presets.Short(Ytdlp.QualityName(cfg.Quality));
            bar.ContainerText = () => Presets.Short(Ytdlp.ContainerName(cfg.Container));
            bar.HudShown = hudShown;
            bar.Add += () => grab();
            bar.PickQuality += p => Pick(p, Ytdlp.Qualities, () => cfg.Quality, k => cfg.Quality = k);
            bar.PickContainer += p => Pick(p, Ytdlp.Containers, () => cfg.Container, k => cfg.Container = k);
            bar.ToggleHud += () => { toggleHud(); bar.Invalidate(); };
            bar.OpenFolder += OpenFolder;
            bar.OpenSettings += openSettings;
            bar.Build();
            foot.Dock = DockStyle.Bottom;
            foot.Height = 28;
            foot.UseCompatibleTextRendering = true;
            foot.Font = Fonts.Body(8.5f);
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
            Controls.Add(bar);

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

        public void RefreshHeader() { bar.Invalidate(); }

        void Pick(Point at, string[][] opts, Func<string> cur, Action<string> set)
        {
            ContextMenuStrip m = Ui.Menu();
            Presets.Fill(m.Items, opts, cur(), k => { set(k); Store.SaveSettings(cfg); bar.Invalidate(); });
            m.Show(bar, at);
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

