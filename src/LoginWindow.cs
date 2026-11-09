using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace OpenD
{
    // Окно входа в YouTube на встроенном Edge (WebView2). После входа cookies сохраняются
    // в %APPDATA%\OpenD\cookies.txt (формат Netscape) — их читает yt-dlp. Расшифровка Chrome не нужна.
    class LoginWindow : Form
    {
        public static readonly string CookieFile = Path.Combine(Store.Dir, "cookies.txt");
        static readonly string Profile = Path.Combine(Store.Dir, "webview");
        const string LoginUrl = "https://accounts.google.com/ServiceLogin?service=youtube&continue=https%3A%2F%2Fwww.youtube.com%2F";

        readonly WebView2 wv = new WebView2();
        readonly Label hint = new Label();
        readonly Action<string> done;   // null — успех, иначе текст ошибки
        static LoginWindow current;

        // Выход: удаляем cookies для yt-dlp и профиль встроенного браузера (иначе Google войдёт под прежним аккаунтом).
        public static bool Logout()
        {
            bool ok = true;
            if (current != null && !current.IsDisposed) { try { current.Close(); } catch { } }
            try { if (File.Exists(CookieFile)) File.Delete(CookieFile); } catch (Exception e) { Log.Error("удаление cookies", e); ok = false; }
            try { if (Directory.Exists(Profile)) Directory.Delete(Profile, true); }
            catch (Exception e) { Log.Error("удаление профиля входа", e); ok = false; }
            Log.Write("выход из YouTube: " + (ok ? "ok" : "не всё удалено"));
            return ok;
        }

        public static void ShowOnce(Action<string> onDone)
        {
            if (current != null && !current.IsDisposed) { current.Activate(); return; }
            try
            {
                current = new LoginWindow(onDone);
                current.Show();
            }
            catch (Exception e)   // например, нет WebView2Loader.dll рядом с exe или повреждён Runtime
            {
                Log.Error("окно входа", e);
                current = null;
                onDone(Explain(e));
            }
        }

        public const string RuntimeUrl = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";

        public static string Explain(Exception e)
        {
            if (e is WebView2RuntimeNotFoundException)
                return "Не найден Microsoft Edge WebView2 Runtime — без него окно входа не открывается.\n\nУстановите его (Evergreen Bootstrapper): " + RuntimeUrl +
                       "\n\nПроще: поставьте OpenD через OpenD-Setup.exe, он делает это сам.";
            if (e is DllNotFoundException || e is System.IO.FileNotFoundException || e is BadImageFormatException)
                return "Не найден файл WebView2 (WebView2Loader.dll / Microsoft.Web.WebView2.*.dll) рядом с OpenD.exe. Распакуйте архив целиком или поставьте OpenD-Setup.exe.\n\n" + e.Message;
            return "Не удалось открыть окно входа: " + e.Message + "\n\nПодробности: " + Log.Path;
        }

        LoginWindow(Action<string> onDone)
        {
            done = onDone;
            Text = "OpenD — вход в YouTube";
            Icon = Native.MakeIcon();
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = new Font("Segoe UI", 9f);
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(980, 720);

            Panel bar = new Panel { Dock = DockStyle.Top, Height = 52, BackColor = Theme.Panel };
            Button ok = Ui.Btn("Готово — сохранить вход", (s, e) => Save());
            ok.Dock = DockStyle.Right;
            hint.Dock = DockStyle.Fill;
            hint.TextAlign = ContentAlignment.MiddleLeft;
            hint.Padding = new Padding(12, 0, 0, 0);
            hint.ForeColor = Theme.Dim;
            hint.Text = "Войдите в аккаунт Google (лучше отдельный, не основной), затем нажмите «Готово».";
            bar.Controls.Add(hint);
            bar.Controls.Add(ok);

            wv.Dock = DockStyle.Fill;
            wv.DefaultBackgroundColor = Theme.Bg;
            wv.CreationProperties = new CoreWebView2CreationProperties { UserDataFolder = Profile };
            Controls.Add(wv);
            Controls.Add(bar);
            Load += (s, e) => Init();
        }

        async void Init()
        {
            try
            {
                await wv.EnsureCoreWebView2Async(null);
                Log.Write("WebView2 " + CoreWebView2Environment.GetAvailableBrowserVersionString());
                wv.CoreWebView2.NavigationCompleted += (s, a) =>
                {
                    if (!a.IsSuccess)
                    {
                        Log.Write("вход: страница не открылась, " + a.WebErrorStatus);
                        hint.Text = "Страница Google не открывается (" + a.WebErrorStatus + "). Проверьте интернет/VPN и нажмите F5.";
                        hint.ForeColor = Theme.Err;
                    }
                };
                wv.CoreWebView2.Navigate(LoginUrl);
                TopMost = true; Activate(); TopMost = false;
            }
            catch (Exception e)
            {
                Log.Error("WebView2", e);
                string msg = Explain(e);
                Close();
                done(msg);
            }
        }

        async void Save()
        {
            try
            {
                int n; bool login;
                List<CoreWebView2Cookie> all = await Collect();
                n = Write(all, out login);
                Log.Write("cookies: экспортировано " + n + ", вход выполнен: " + login);
                if (!login)
                {
                    hint.Text = "Вход не обнаружен. Войдите в аккаунт на странице и нажмите «Готово» снова.";
                    hint.ForeColor = Theme.Err;
                    return;
                }
                Close();
                done(null);
            }
            catch (Exception e)
            {
                Log.Error("экспорт cookies", e);
                hint.Text = "Ошибка: " + e.Message;
                hint.ForeColor = Theme.Err;
            }
        }

        async System.Threading.Tasks.Task<List<CoreWebView2Cookie>> Collect()
        {
            CoreWebView2CookieManager cm = wv.CoreWebView2.CookieManager;
            Dictionary<string, CoreWebView2Cookie> map = new Dictionary<string, CoreWebView2Cookie>();
            string[] urls =
            {
                "https://www.youtube.com", "https://accounts.google.com", "https://accounts.youtube.com",
                "https://www.google.com", "https://myaccount.google.com"
            };
            foreach (string u in urls)
                foreach (CoreWebView2Cookie c in await cm.GetCookiesAsync(u))
                    map[c.Domain + "|" + c.Path + "|" + c.Name] = c;
            return new List<CoreWebView2Cookie>(map.Values);
        }

        static int Write(List<CoreWebView2Cookie> cookies, out bool login)
        {
            login = false;
            long yearAhead = DateTimeOffset.UtcNow.AddYears(1).ToUnixTimeSeconds();
            StringBuilder sb = new StringBuilder("# Netscape HTTP Cookie File\n");
            foreach (CoreWebView2Cookie c in cookies)
            {
                if (c.Name == "SAPISID" || c.Name == "__Secure-3PSID") login = true;
                long exp = c.IsSession ? yearAhead : new DateTimeOffset(c.Expires.ToUniversalTime()).ToUnixTimeSeconds();
                sb.Append(c.IsHttpOnly ? "#HttpOnly_" : "").Append(c.Domain).Append('\t')
                  .Append(c.Domain.StartsWith(".") ? "TRUE" : "FALSE").Append('\t')
                  .Append(string.IsNullOrEmpty(c.Path) ? "/" : c.Path).Append('\t')
                  .Append(c.IsSecure ? "TRUE" : "FALSE").Append('\t')
                  .Append(exp).Append('\t').Append(c.Name).Append('\t').Append(c.Value).Append('\n');
            }
            Directory.CreateDirectory(Store.Dir);
            File.WriteAllText(CookieFile, sb.ToString(), new UTF8Encoding(false));
            return cookies.Count;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.Style(Handle, false);
        }
    }
}
