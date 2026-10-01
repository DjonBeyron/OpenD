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
        public static bool Ready { get { return File.Exists(YtDlp) && File.Exists(Ffmpeg); } }

        const string UrlYtDlp = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
        const string UrlFfmpeg = "https://github.com/yt-dlp/FFmpeg-Builds/releases/latest/download/ffmpeg-master-latest-win64-gpl.zip";
        const string UrlDeno = "https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip";

        // Блокирующий вызов: запускать в фоновом потоке. Возвращает true, если всё готово.
        public static bool Ensure(Action<string> status)
        {
            try
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;   // TLS 1.2
                Directory.CreateDirectory(Store.Bin);
                if (!File.Exists(YtDlp)) Fetch(UrlYtDlp, YtDlp, "yt-dlp", status);
                if (!File.Exists(Ffmpeg))
                {
                    string zip = Path.Combine(Store.Bin, "ffmpeg.zip");
                    Fetch(UrlFfmpeg, zip, "ffmpeg", status);
                    status("Распаковка ffmpeg…");
                    Unzip(zip, "ffmpeg.exe", Ffmpeg);
                    Unzip(zip, "ffprobe.exe", Path.Combine(Store.Bin, "ffprobe.exe"));
                    File.Delete(zip);
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
                    catch (Exception e) { Log.Error("deno", e); }   // без deno часть форматов YouTube может быть недоступна, но загрузка работает
                }
                return Ready;
            }
            catch (Exception e)
            {
                Log.Error("загрузка компонентов", e);
                status("Не удалось загрузить компоненты: " + e.Message);
                return false;
            }
        }

        static void Fetch(string url, string dest, string name, Action<string> status)
        {
            string tmp = dest + ".part";
            Exception err = null;
            using (WebClient wc = new WebClient())
            using (ManualResetEvent done = new ManualResetEvent(false))
            {
                wc.Headers[HttpRequestHeader.UserAgent] = "OpenD";
                wc.DownloadProgressChanged += (s, e) => status("Загрузка " + name + " " + e.ProgressPercentage + "%");
                wc.DownloadFileCompleted += (s, e) => { err = e.Error; done.Set(); };
                status("Загрузка " + name + "…");
                wc.DownloadFileAsync(new Uri(url), tmp);
                done.WaitOne();
            }
            if (err != null)
            {
                try { File.Delete(tmp); } catch { }
                throw err;
            }
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
