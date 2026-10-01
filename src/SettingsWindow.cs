using System;
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
            public Action Folder;
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
            ClientSize = new Size(470, 520);

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
            Label folder = Ui.Text(h.FolderText(), true, 9f);
            folder.AutoSize = false; folder.Width = 420; folder.Height = 22;
            col.Controls.Add(folder);
            col.Controls.Add(Ui.Btn("Изменить папку…", (s, e) => { h.Folder(); folder.Text = h.FolderText(); }));

            col.Controls.Add(Header("Аккаунт"));
            FlowLayoutPanel row = new FlowLayoutPanel { AutoSize = true, BackColor = Theme.Bg, Margin = new Padding(0) };
            row.Controls.Add(Ui.Btn("Войти в YouTube…", (s, e) => h.Login()));
            row.Controls.Add(Ui.Btn("Открыть лог", (s, e) => Log.Open()));
            col.Controls.Add(row);
            Controls.Add(col);
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
