using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Threading;

namespace OpenD
{
    // Загрузка и обновление yt-dlp, ffmpeg и deno (JS-движок для YouTube) в %APPDATA%\OpenD\bin.
    static class Tools
    {
        public static string YtDlp { get { return Path.Combine(Store.Bin, "yt-dlp.exe"); } }
        public static string Ffmpeg { get { return Path.Combine(Store.Bin, "ffmpeg.exe"); } }
        public static string Deno { get { return Path.Combine(Store.Bin, "deno.exe"); } }
        public static string Proxy;       // из настроек (необязательно)
        public static int Progress = -1;  // общий прогресс загрузки компонентов, 0..100 (-1 — не идёт)
        static int stages, stageDone;
        public static string LastError;   // последняя причина сбоя — показывается вместо голого «Жду интернет»
        public static bool Ready { get { return File.Exists(YtDlp) && File.Exists(Ffmpeg); } }

        const string UrlYtDlp = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
        const string UrlFfmpeg = "https://github.com/yt-dlp/FFmpeg-Builds/releases/latest/download/ffmpeg-master-latest-win64-gpl.zip";
        const string UrlDeno = "https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip";

        // Блокирующий вызов: запускать в фоновом потоке. Возвращает true, если всё готово.
        public static bool Ensure(Action<string> status)
        {
            try
            {
                try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072 | (SecurityProtocolType)12288; }   // TLS 1.2 + 1.3
                catch { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; }
                ServicePointManager.Expect100Continue = false;
                LastError = null;
                stages = (File.Exists(YtDlp) ? 0 : 1) + (File.Exists(Ffmpeg) ? 0 : 1) + (File.Exists(Deno) ? 0 : 1);
                stageDone = 0; Progress = stages > 0 ? 0 : -1;
                Directory.CreateDirectory(Store.Bin);
                if (!File.Exists(YtDlp)) { Fetch(UrlYtDlp, YtDlp, "yt-dlp", status); stageDone++; }
                if (!File.Exists(Ffmpeg))
                {
                    string zip = Path.Combine(Store.Bin, "ffmpeg.zip");
                    Fetch(UrlFfmpeg, zip, "ffmpeg", status);
                    status("Распаковка ffmpeg…");
                    Unzip(zip, "ffmpeg.exe", Ffmpeg);
                    Unzip(zip, "ffprobe.exe", Path.Combine(Store.Bin, "ffprobe.exe"));
                    File.Delete(zip);
                    stageDone++;
                }
                if (!File.Exists(Deno))
                {
                    try
                    {
                        string zip = Path.Combine(Store.Bin, "deno.zip");
                        Fetch(UrlDeno, zip, "deno", status);
                        Unzip(zip, "deno.exe", Deno);
                        File.Delete(zip);
                    }
                    catch (Exception e) { Log.Error("deno", e); }
                    stageDone++;   // без deno часть форматов YouTube может быть недоступна, но загрузка работает
                }
                Progress = -1;
                return Ready;
            }
            catch (Exception e)
            {
                Log.Error("загрузка компонентов", e);
                Exception r = e; while (r.InnerException != null) r = r.InnerException;
                LastError = r.Message;
                Progress = -1;
                status("Не удалось загрузить компоненты: " + r.Message);
                return false;
            }
        }

        static IWebProxy MakeProxy()
        {
            if (!string.IsNullOrEmpty(Proxy)) { try { return new WebProxy(Proxy); } catch { } }
            IWebProxy p = WebRequest.GetSystemWebProxy();   // системный прокси / VPN-расширения
            p.Credentials = CredentialCache.DefaultCredentials;
            return p;
        }

        // Скачивание с таймаутами (старый WebClient мог висеть бесконечно), 3 попытки, прокси из системы/настроек.
        static void Fetch(string url, string dest, string name, Action<string> status)
        {
            string tmp = dest + ".part";
            Exception err = null;
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    status("Загрузка " + name + "…");
                    HttpWebRequest rq = (HttpWebRequest)WebRequest.Create(url);
                    rq.UserAgent = "OpenD";
                    rq.Timeout = 30000;
                    rq.ReadWriteTimeout = 30000;
                    rq.Proxy = MakeProxy();
                    rq.AllowAutoRedirect = true;
                    using (HttpWebResponse rs = (HttpWebResponse)rq.GetResponse())
                    using (Stream src = rs.GetResponseStream())
                    using (FileStream dst = new FileStream(tmp, FileMode.Create, FileAccess.Write))
                    {
                        long total = rs.ContentLength, got = 0;
                        byte[] buf = new byte[81920];
                        int n, last = -1;
                        while ((n = src.Read(buf, 0, buf.Length)) > 0)
                        {
                            dst.Write(buf, 0, n);
                            got += n;
                            if (total > 0)
                            {
                                int pc = (int)(got * 100 / total);
                                if (pc != last)
                                {
                                    last = pc;
                                    if (stages > 0) Progress = Math.Min(99, (stageDone * 100 + pc) / stages);
                                    status("Загрузка " + name + " " + pc + "%" + (stages > 1 ? "  (" + Math.Min(stages, stageDone + 1) + "/" + stages + ")" : ""));
                                }
                            }
                        }
                    }
                    err = null;
                    break;
                }
                catch (Exception e)
                {
                    err = e;
                    Log.Error("загрузка " + name + " (попытка " + attempt + ")", e);
                    try { File.Delete(tmp); } catch { }
                    if (attempt < 3) Thread.Sleep(2000 * attempt);
                }
            }
            if (err != null) throw err;
            if (File.Exists(dest)) File.Delete(dest);
            File.Move(tmp, dest);
        }

        static void Unzip(string zip, string fileName, string dest)
        {
            using (ZipArchive z = ZipFile.OpenRead(zip))
                foreach (ZipArchiveEntry e in z.Entries)
                    if (string.Equals(e.Name, fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        e.ExtractToFile(dest, true);
                        return;
                    }
            throw new FileNotFoundException(fileName + " не найден в архиве");
        }

        // YouTube часто меняется — держим yt-dlp свежим.
        public static void Update()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(YtDlp, "-U")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (Process p = Process.Start(psi))
                {
                    p.OutputDataReceived += (s, e) => { if (e.Data != null) Log.Write("[update] " + e.Data); };
                    p.ErrorDataReceived += (s, e) => { if (e.Data != null) Log.Write("[update] " + e.Data); };
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    if (!p.WaitForExit(90000)) { try { p.Kill(); } catch { } }
                }
            }
            catch (Exception e) { Log.Error("обновление yt-dlp", e); }
        }
    }
}
