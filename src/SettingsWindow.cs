using System;
using System.Drawing;
using System.Windows.Forms;

namespace OpenD
{
    // Окно «Настройки»: автозапуск, запуск в трее, слежение за буфером, папка, вход в YouTube.
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
        }

        public SettingsWindow(Settings cfg, Hooks h)
        {
            Text = "OpenD — настройки";
            Icon = Native.MakeIcon();
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(440, 330);

            FlowLayoutPanel col = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false,
                Padding = new Padding(18, 14, 18, 8), BackColor = Theme.Bg
            };
            col.Controls.Add(Check("Запускать вместе с Windows", h.GetAutostart(), v => h.SetAutostart(v)));
            col.Controls.Add(Check("Запускать свёрнутым в трей (без окна)", cfg.StartInTray,
                v => { cfg.StartInTray = v; Store.SaveSettings(cfg); }));
            col.Controls.Add(Check("Следить за буфером обмена (добавлять ссылки сами)", cfg.WatchClipboard,
                v => { h.SetWatch(v); Store.SaveSettings(cfg); }));

            Label folder = new Label { AutoSize = true, ForeColor = Theme.Dim, Margin = new Padding(3, 14, 3, 4) };
            folder.Text = "Папка загрузки: " + h.FolderText();
            col.Controls.Add(folder);
            col.Controls.Add(Ui.Btn("Изменить папку…", (s, e) => { h.Folder(); folder.Text = "Папка загрузки: " + h.FolderText(); }));
            col.Controls.Add(new Label
            {
                AutoSize = true, ForeColor = Theme.Dim, Margin = new Padding(3, 14, 3, 4),
                Text = "Горячая клавиша: " + cfg.HotkeyText() + "  (меняется в settings.json)"
            });
            FlowLayoutPanel row = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 10, 0, 0), BackColor = Theme.Bg };
            row.Controls.Add(Ui.Btn("Войти в YouTube…", (s, e) => h.Login()));
            row.Controls.Add(Ui.Btn("Открыть лог", (s, e) => Log.Open()));
            col.Controls.Add(row);
            Controls.Add(col);
        }

        static CheckBox Check(string text, bool value, Action<bool> changed)
        {
            CheckBox c = new CheckBox
            {
                Text = text, Checked = value, AutoSize = true, ForeColor = Theme.Text,
                FlatStyle = FlatStyle.Flat, Margin = new Padding(3, 6, 3, 6), Cursor = Cursors.Hand
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
