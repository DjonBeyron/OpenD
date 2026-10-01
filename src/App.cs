using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace OpenD
{
    // Невидимое окно для горячей клавиши и слежения за буфером обмена.
    class Sink : NativeWindow
    {
        public event Action<int> Hotkey;
        public event Action Clip;

        public Sink()
        {
            CreateHandle(new CreateParams { Parent = (IntPtr)(-3) });   // HWND_MESSAGE
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_HOTKEY && Hotkey != null) Hotkey((int)m.WParam);
            else if (m.Msg == Native.WM_CLIPBOARDUPDATE && Clip != null) Clip();
            base.WndProc(ref m);
        }
    }

    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            bool first;
            using (Mutex mutex = new Mutex(true, @"Local\OpenD.single", out first))
            {
                EventWaitHandle show = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\OpenD.show");
                if (!first) { show.Set(); return; }
                App.AutoStarted = Array.IndexOf(args, "--autostart") >= 0;
                Log.Write("=== OpenD запущен, " + Environment.OSVersion + " ===");
                Fonts.Init();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (s, e) => { Log.Error("UI", e.Exception); App.Fatal(e.Exception); };
                AppDomain.CurrentDomain.UnhandledException += (s, e) => { Log.Error("поток", e.ExceptionObject as Exception); App.Fatal(e.ExceptionObject as Exception); };
                try { new App(show).Run(); }
                catch (Exception e) { Log.Error("запуск", e); App.Fatal(e); }
            }
        }
    }

    class App
    {
        // Ошибки в UI не роняют программу молча: показываем и пишем в лог.
        public static void Fatal(Exception e)
        {
            MessageBox.Show((e == null ? "Неизвестная ошибка" : e.GetType().Name + ": " + e.Message) +
                "\n\nПодробности: " + Log.Path, "OpenD — ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public static bool AutoStarted;
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        readonly Settings cfg = Store.LoadSettings();
        readonly bool firstRun = !Store.SettingsExist();
        readonly Engine eng;
        readonly Hud hud;
        readonly MainWindow win;
        readonly Sink sink = new Sink();
        readonly NotifyIcon tray = new NotifyIcon();
        readonly EventWaitHandle showEvt;
        bool watching;
        SynchronizationContext ui;

        public App(EventWaitHandle showEvent)
        {
            showEvt = showEvent;
            eng = new Engine(cfg);
            hud = new Hud(eng, cfg);
            win = new MainWindow(eng, cfg, () => Grab(true), OpenSettings, () => hud.Toggle(), () => hud.IsShown);
            ui = SynchronizationContext.Current;
        }

        public void Run()
        {
            tray.Icon = Native.MakeIcon();
            tray.Text = "OpenD — " + cfg.HotkeyText();
            tray.ContextMenuStrip = BuildMenu();
            tray.MouseUp += (s, e) => { if (e.Button == MouseButtons.Left) win.ShowFront(); };
            tray.Visible = true;

            sink.Hotkey += id => { if (id == 1) Grab(true); else if (id == 2) hud.Toggle(); };
            sink.Clip += () => Grab(false);
            if (!Native.RegisterHotKey(sink.Handle, 1, cfg.Mods | Native.MOD_NOREPEAT, (uint)cfg.Key))
                hud.Pop("Сочетание " + cfg.HotkeyText() + " занято — выберите другое в настройках");
            if (!Native.RegisterHotKey(sink.Handle, 2, cfg.HudMods | Native.MOD_NOREPEAT, (uint)cfg.HudKey))
                hud.Pop("Сочетание " + Settings.HotkeyText(cfg.HudMods, cfg.HudKey) + " занято — выберите другое в настройках");
            SetWatch(cfg.WatchClipboard);

            eng.Finished += it => ui.Post(o => hud.Pop(it.State == St.Done
                ? "Готово: " + Short(it.Title) : "Ошибка: " + Short(it.Error ?? it.Title)), null);
            new Thread(() => { while (showEvt.WaitOne()) ui.Post(o => win.ShowFront(), null); })
                { IsBackground = true }.Start();
            new Thread(() => { try { Prepare(); } catch (Exception e) { Log.Error("подготовка", e); eng.Note = null; ui.Post(o => hud.Pop("Ошибка запуска, см. лог"), null); } }) { IsBackground = true }.Start();

            if (!firstRun && !AutoStarted) hud.Pop("OpenD работает в трее · " + cfg.HotkeyText());
            if (!cfg.StartInTray && !firstRun) win.ShowFront();
            if (firstRun)
            {
                cfg.Seen = true; Store.SaveSettings(cfg);
                win.ShowFront();
                hud.Pop("Скопируйте ссылку и нажмите " + cfg.HotkeyText());
            }
            Application.Run();
        }

        static string Short(string t)
        {
            t = string.IsNullOrEmpty(t) ? "видео" : t;
            return t.Length > 40 ? t.Substring(0, 40) + "…" : t;
        }

        void Prepare()
        {
            bool fresh = !Tools.Ready;
            if (fresh) ui.Post(o => hud.Pop(null), null);
            // Нет интернета при первом запуске: ждём и пробуем снова, пока компоненты не скачаются.
            while (!Tools.Ensure(n => { eng.Note = n; }))
            {
                eng.Note = "Жду интернет…";
                Thread.Sleep(20000);
            }
            if (!fresh && DateTime.UtcNow.Ticks - cfg.LastUpdate > TimeSpan.TicksPerDay)
            {
                eng.Note = "Обновление yt-dlp…";
                Tools.Update();
            }
            if (fresh || DateTime.UtcNow.Ticks - cfg.LastUpdate > TimeSpan.TicksPerDay)
            {
                cfg.LastUpdate = DateTime.UtcNow.Ticks; Store.SaveSettings(cfg);
            }
            eng.Note = null;
            eng.Start();
            if (eng.Pending() > 0) ui.Post(o => hud.Pop("Продолжаю загрузки"), null);
        }

        // fromHotkey: true — ответить даже если ссылки нет; false — тихий режим слежения.
        void Grab(bool fromHotkey)
        {
            string text = ReadClipboard();
            Log.Write("буфер: " + (text == null ? "(пусто)" : (text.Length > 200 ? text.Substring(0, 200) : text)));
            string id = Yt.Id(text);
            if (id == null)
            {
                if (fromHotkey) hud.Pop("В буфере нет ссылки на YouTube");
                return;
            }
            string msg;
            Item it = eng.Add(id, out msg);
            if (it == null) { if (fromHotkey) hud.Pop(msg); return; }
            hud.Pop("Добавлено в очередь");
        }

        static string ReadClipboard()
        {
            for (int i = 0; i < 5; i++)
            {
                try { return Clipboard.ContainsText() ? Clipboard.GetText() : null; }
                catch (ExternalException) { Thread.Sleep(40); }
            }
            return null;
        }

        void SetWatch(bool on)
        {
            if (on == watching) return;
            watching = on;
            if (on) Native.AddClipboardFormatListener(sink.Handle);
            else Native.RemoveClipboardFormatListener(sink.Handle);
            cfg.WatchClipboard = on;
        }

        ContextMenuStrip BuildMenu()
        {
            ContextMenuStrip m = Ui.Menu();
            Ui.Add(m, "Открыть", (s, e) => win.ShowFront());
            Ui.Add(m, "Скачать из буфера   " + cfg.HotkeyText(), (s, e) => Grab(true));
            Ui.Add(m, "Мини-окно: показать / скрыть   " + Settings.HotkeyText(cfg.HudMods, cfg.HudKey), (s, e) => hud.Toggle());
            m.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem w = Ui.Add(m, "Следить за буфером", null);
            w.Checked = cfg.WatchClipboard;
            w.Click += (s, e) => { SetWatch(!watching); w.Checked = watching; Store.SaveSettings(cfg); };
            m.Items.Add(Presets.Submenu("Качество", Ytdlp.Qualities, () => cfg.Quality, SetQuality));
            m.Items.Add(Presets.Submenu("Формат (контейнер)", Ytdlp.Containers, () => cfg.Container, SetContainer));
            ToolStripMenuItem au = Ui.Add(m, "Запускать с Windows", null);
            au.Checked = GetAutostart();
            au.Click += (s, e) => { SetAutostart(!GetAutostart()); au.Checked = GetAutostart(); };
            Ui.Add(m, "Настройки…", (s, e) => OpenSettings());
            Ui.Add(m, "Папка загрузки…", (s, e) => PickFolder());
            Ui.Add(m, "Войти в YouTube…", (s, e) => Login());
            m.Items.Add(CookieMenu());
            Ui.Add(m, "Открыть лог", (s, e) => Log.Open());
            m.Items.Add(new ToolStripSeparator());
            Ui.Add(m, "Выход", (s, e) => Quit());
            return m;
        }

        void SetQuality(string k) { cfg.Quality = k; Store.SaveSettings(cfg); win.RefreshHeader(); }

        void SetContainer(string k) { cfg.Container = k; Store.SaveSettings(cfg); win.RefreshHeader(); }

        // Переназначение горячей клавиши: пробуем зарегистрировать новое сочетание, при неудаче возвращаем прежнее.
        bool ApplyKey(int id, uint mods, int key)
        {
            uint oldM = id == 1 ? cfg.Mods : cfg.HudMods;
            int oldK = id == 1 ? cfg.Key : cfg.HudKey;
            uint otherM = id == 1 ? cfg.HudMods : cfg.Mods;
            int otherK = id == 1 ? cfg.HudKey : cfg.Key;
            if (mods == otherM && key == otherK) return false;
            Native.UnregisterHotKey(sink.Handle, id);
            if (Native.RegisterHotKey(sink.Handle, id, mods | Native.MOD_NOREPEAT, (uint)key))
            {
                if (id == 1) { cfg.Mods = mods; cfg.Key = key; } else { cfg.HudMods = mods; cfg.HudKey = key; }
                Store.SaveSettings(cfg);
                tray.Text = "OpenD — " + cfg.HotkeyText();
                Log.Write("горячая клавиша " + id + ": " + Settings.HotkeyText(mods, key));
                return true;
            }
            Native.RegisterHotKey(sink.Handle, id, oldM | Native.MOD_NOREPEAT, (uint)oldK);
            return false;
        }

        void OpenSettings()
        {
            SettingsWindow.Hooks h = new SettingsWindow.Hooks
            {
                GetAutostart = GetAutostart,
                SetAutostart = SetAutostart,
                SetWatch = SetWatch,
                Login = Login,
                Folder = PickFolder,
                FolderText = () => cfg.Folder,
                ApplyGrabKey = (m, k) => ApplyKey(1, m, k),
                ApplyHudKey = (m, k) => ApplyKey(2, m, k)
            };
            using (SettingsWindow w = new SettingsWindow(cfg, h)) w.ShowDialog();
            Store.SaveSettings(cfg);
            tray.ContextMenuStrip = BuildMenu();
        }

        void Login()
        {
            LoginWindow.ShowOnce(err =>
            {
                if (err != null) { MessageBox.Show(err, "OpenD"); return; }
                cfg.Cookies = "file";
                Store.SaveSettings(cfg);
                tray.ContextMenuStrip = BuildMenu();
                eng.ResumeFailed();
                hud.Pop("Вход сохранён. Перезапускаю загрузки");
            });
        }

        // Если YouTube просит «подтвердите, что вы не бот» — нужны cookies браузера, где вы вошли в YouTube.
        ToolStripMenuItem CookieMenu()
        {
            ToolStripMenuItem root = new ToolStripMenuItem("Cookies (вход в YouTube)");
            root.ForeColor = Theme.Text;
            root.DropDown.BackColor = Theme.Panel;
            root.DropDown.Font = Fonts.Gdi(9f);
            root.DropDown.ForeColor = Theme.Text;
            ((ToolStripDropDownMenu)root.DropDown).ShowImageMargin = false;
            ((ToolStripDropDownMenu)root.DropDown).Renderer = new ToolStripProfessionalRenderer(new DarkColors());
            string[][] opts =
            {
                new[] { "", "Не использовать" }, new[] { "firefox", "Firefox (надёжнее всего)" },
                new[] { "edge", "Edge (обычно не работает)" }, new[] { "chrome", "Chrome (обычно не работает)" }, new[] { "brave", "Brave (обычно не работает)" },
                new[] { "file", "Файл cookies.txt…" }
            };
            foreach (string[] o in opts)
            {
                string key = o[0];
                ToolStripMenuItem it = new ToolStripMenuItem(o[1]) { ForeColor = Theme.Text, Checked = (cfg.Cookies ?? "") == key };
                it.Click += (s, e) =>
                {
                    if (key == "file" && !File.Exists(Path.Combine(Store.Dir, "cookies.txt")))
                    {
                        Directory.CreateDirectory(Store.Dir);
                        MessageBox.Show("Экспортируйте cookies YouTube расширением «Get cookies.txt LOCALLY» и сохраните файл как:\n\n" +
                            Path.Combine(Store.Dir, "cookies.txt") + "\n\nПосле этого выберите этот пункт снова.", "OpenD");
                        Process.Start("explorer.exe", "\"" + Store.Dir + "\"");
                        return;
                    }
                    cfg.Cookies = key;
                    Store.SaveSettings(cfg);
                    foreach (ToolStripMenuItem x in root.DropDownItems) x.Checked = false;
                    it.Checked = true;
                    Log.Write("cookies: " + (key == "" ? "нет" : key));
                    eng.ResumeFailed();
                    hud.Pop("Cookies: " + (key == "" ? "выключены" : o[1]) + ". Перезапускаю загрузки");
                };
                root.DropDownItems.Add(it);
            }
            return root;
        }

        void PickFolder()
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog { SelectedPath = cfg.Folder, Description = "Куда сохранять видео" })
                if (d.ShowDialog() == DialogResult.OK) { cfg.Folder = d.SelectedPath; Store.SaveSettings(cfg); }
        }

        public static bool GetAutostart()
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                return k != null && k.GetValue("OpenD") != null;
        }

        public static void SetAutostart(bool on)
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
            {
                if (k == null) return;
                if (on) k.SetValue("OpenD", "\"" + Application.ExecutablePath + "\" --autostart");
                else k.DeleteValue("OpenD", false);
            }
        }

        void Quit()
        {
            Native.UnregisterHotKey(sink.Handle, 1);
            Native.UnregisterHotKey(sink.Handle, 2);
            if (watching) Native.RemoveClipboardFormatListener(sink.Handle);
            eng.Shutdown();
            Store.SaveSettings(cfg);
            tray.Visible = false;
            win.Quitting = true;
            Application.Exit();
        }
    }
}
