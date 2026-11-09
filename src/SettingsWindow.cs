using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace OpenD
{
    // Окно «Настройки»: запуск, мини-окно, горячие клавиши, папка, вход в YouTube.
    class SettingsWindow : Form
    {
        public class Hooks
        {
            public Func<bool> GetAutostart;
            public Action<bool> SetAutostart;
            public Action<bool> SetWatch;
            public Action Login;
            public Action Logout;
            public Action Switch;
            public Action Folder;
            public Action<string> SetQuality;
            public Action<string> SetContainer;
            public Func<bool> SignedIn;
            public Func<string> FolderText;
            public Func<uint, int, bool> ApplyGrabKey;      // новое сочетание «скачать из буфера»
            public Func<uint, int, bool> ApplyHudKey;       // новое сочетание «показать/скрыть мини-окно»
        }

        public SettingsWindow(Settings cfg, Hooks h)
        {
            Text = "OpenD " + AppInfo.Version + " — настройки";
            Icon = Native.MakeIcon();
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Fonts.Body(9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(470, 700);

            FlowLayoutPanel col = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false,
                Padding = new Padding(22, 16, 22, 8), BackColor = Theme.Bg
            };
            col.Controls.Add(Header("Запуск"));
            col.Controls.Add(Check("Запускать вместе с Windows", h.GetAutostart(), v => h.SetAutostart(v)));
            col.Controls.Add(Check("Запускать свёрнутым в трей (без окна)", cfg.StartInTray,
                v => { cfg.StartInTray = v; Store.SaveSettings(cfg); }));
            col.Controls.Add(Check("Следить за буфером обмена (добавлять ссылки сами)", cfg.WatchClipboard,
                v => { h.SetWatch(v); Store.SaveSettings(cfg); }));

            col.Controls.Add(Header("Мини-окно"));
            col.Controls.Add(Check("Показывать мини-окно при загрузках", cfg.HudAuto,
                v => { cfg.HudAuto = v; Store.SaveSettings(cfg); }));

            col.Controls.Add(Header("Горячие клавиши"));
            col.Controls.Add(KeyRow("Скачать ссылку из буфера", new HotkeyBox(cfg.Mods, cfg.Key, h.ApplyGrabKey)));
            col.Controls.Add(KeyRow("Показать / скрыть мини-окно", new HotkeyBox(cfg.HudMods, cfg.HudKey, h.ApplyHudKey)));

            col.Controls.Add(Header("Загрузки"));
            col.Controls.Add(PickRow("Качество", Ytdlp.Qualities, cfg.Quality, v => h.SetQuality(v)));
            col.Controls.Add(PickRow("Формат (контейнер)", Ytdlp.Containers, cfg.Container, v => h.SetContainer(v)));
            Label folder = Ui.Text(h.FolderText(), true, 9f);
            folder.AutoSize = false; folder.Width = 420; folder.Height = 22;
            col.Controls.Add(folder);
            FlowLayoutPanel frow = new FlowLayoutPanel { AutoSize = true, BackColor = Theme.Bg, Margin = new Padding(0) };
            frow.Controls.Add(Ui.Btn("Изменить папку…", (s, e) => { h.Folder(); folder.Text = h.FolderText(); }));
            frow.Controls.Add(Ui.Btn("Открыть папку", (s, e) => { try { Process.Start("explorer.exe", "\"" + h.FolderText() + "\""); } catch { } }));
            col.Controls.Add(frow);

            col.Controls.Add(Header("Аккаунт YouTube"));
            Label st = Ui.Text(h.SignedIn() ? "Вход выполнен" : "Вход не выполнен", true, 9f);
            st.ForeColor = h.SignedIn() ? Theme.Ok : Theme.Warn;
            col.Controls.Add(st);
            FlowLayoutPanel row = new FlowLayoutPanel { AutoSize = true, BackColor = Theme.Bg, Margin = new Padding(0) };
            Action refresh = () => { bool on = h.SignedIn(); st.Text = on ? "Вход выполнен" : "Вход не выполнен"; st.ForeColor = on ? Theme.Ok : Theme.Warn; };
            row.Controls.Add(Ui.Btn("Войти…", (s, e) => { h.Login(); refresh(); }));
            row.Controls.Add(Ui.Btn("Сменить аккаунт…", (s, e) => { h.Switch(); refresh(); }));
            row.Controls.Add(Ui.Btn("Выйти", (s, e) => { h.Logout(); refresh(); }));
            col.Controls.Add(row);
            Timer poll = new Timer { Interval = 1000 };     // вход завершается в другом окне — подхватываем статус
            poll.Tick += (s, e) => refresh();
            poll.Start();
            Disposed += (s, e) => poll.Dispose();

            col.Controls.Add(Header("Диагностика"));
            col.Controls.Add(Ui.Btn("Открыть лог", (s, e) => Log.Open()));
            Controls.Add(col);
        }

        static Control PickRow(string label, string[][] opts, string current, Action<string> set)
        {
            FlowLayoutPanel row = new FlowLayoutPanel { AutoSize = true, BackColor = Theme.Bg, Margin = new Padding(0, 2, 0, 2) };
            Label l = Ui.Text(label, false);
            l.AutoSize = false; l.Width = 175; l.Height = 28; l.TextAlign = ContentAlignment.MiddleLeft;
            ComboBox cb = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList, FlatStyle = FlatStyle.Flat, Width = 240,
                BackColor = Theme.Panel, ForeColor = Theme.Text, Font = Fonts.Body(9f)
            };
            int sel = 0;
            for (int i = 0; i < opts.Length; i++) { cb.Items.Add(opts[i][1]); if (opts[i][0] == current) sel = i; }
            cb.SelectedIndex = sel;
            cb.SelectedIndexChanged += (s, e) => set(opts[cb.SelectedIndex][0]);
            row.Controls.Add(l);
            row.Controls.Add(cb);
            return row;
        }

        static Label Header(string text)
        {
            Label l = Ui.Text(text.ToUpperInvariant(), true, 8f);
            l.Margin = new Padding(3, 16, 3, 2);
            l.ForeColor = Theme.Accent;
            return l;
        }

        static Control KeyRow(string label, HotkeyBox box)
        {
            FlowLayoutPanel row = new FlowLayoutPanel { AutoSize = true, BackColor = Theme.Bg, Margin = new Padding(0, 2, 0, 2) };
            Label l = Ui.Text(label, false);
            l.AutoSize = false; l.Width = 245; l.Height = 32; l.TextAlign = ContentAlignment.MiddleLeft;
            box.Width = 170;
            row.Controls.Add(l);
            row.Controls.Add(box);
            return row;
        }

        static CheckBox Check(string text, bool value, Action<bool> changed)
        {
            CheckBox c = new CheckBox
            {
                UseCompatibleTextRendering = true, Font = Fonts.Body(9.5f),
                Text = text, Checked = value, AutoSize = true, ForeColor = Theme.Text,
                FlatStyle = FlatStyle.Flat, Margin = new Padding(3, 5, 3, 5), Cursor = Cursors.Hand
            };
            c.FlatAppearance.BorderColor = Theme.Dim;
            c.FlatAppearance.CheckedBackColor = Theme.Accent;
            c.CheckedChanged += (s, e) => changed(c.Checked);
            return c;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.Style(Handle, false);
        }
    }
}
