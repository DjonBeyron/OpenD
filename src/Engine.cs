using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;

namespace OpenD
{
    // Очередь загрузок: хранится на диске, после перезапуска продолжается с .part-файлов.
    class Engine
    {
        const int MaxParallel = 2;

        readonly object gate = new object();
        readonly List<Item> items;
        readonly Dictionary<string, Process> procs = new Dictionary<string, Process>();
        readonly HashSet<string> removed = new HashSet<string>();
        readonly Dictionary<string, St> halt = new Dictionary<string, St>();
        readonly Settings cfg;
        bool started, closing;
        System.Threading.Timer retryTimer;
        readonly string demoFile;

        public string Note;                 // состояние компонентов, показывается в HUD
        public event Action Changed;        // изменилась структура очереди / статус
        public event Action<Item> Finished; // загрузка завершилась (успех или ошибка)

        public Engine(Settings s)
        {
            cfg = s;
            demoFile = Environment.GetEnvironmentVariable("OPEND_DEMO_QUEUE");   // только для проверки внешнего вида
            items = demoFile != null ? Store.LoadQueueFrom(demoFile) : Store.LoadQueue();
            foreach (Item it in items)
            {
                if (it.State == St.Active && demoFile == null) { it.State = St.Queued; it.Speed = null; it.Eta = null; }
                if (demoFile != null) { it.LastTick = Environment.TickCount; if (it.RetryAt == 1) it.RetryAt = DateTime.UtcNow.AddSeconds(30).Ticks; continue; }
                it.RetryAt = 0; it.Missing = false;
            }
            NetworkChange.NetworkAvailabilityChanged += (o, e) => { if (e.IsAvailable) NetworkBack(); };
            NetworkChange.NetworkAddressChanged += (o, e) => NetworkBack();
            retryTimer = new System.Threading.Timer(o => { lock (gate) if (items.Any(i => i.State == St.Queued && i.RetryAt > 0)) Pump(); }, null, 3000, 3000);
        }

        // Сеть вернулась — не ждать таймера, сразу пробовать снова.
        void NetworkBack()
        {
            lock (gate)
            {
                bool any = false;
                foreach (Item it in items)
                    if (it.State == St.Queued && it.RetryAt > 0) { it.RetryAt = 0; any = true; }
                if (any) { Log.Write("сеть изменилась — повторяю ожидающие загрузки"); Pump(); }
            }
        }

        static readonly string[] NetErrors =
        {
            "getaddrinfo", "Failed to resolve", "name resolution", "timed out", "Timeout", "Connection reset",
            "Connection aborted", "Connection refused", "Network is unreachable", "urlopen error", "Unable to download",
            "Unable to connect", "SSL:", "EOF occurred", "IncompleteRead", "Remote end closed", "Temporary failure",
            "HTTP Error 5", "Read timed out", "No route to host", "Name or service not known", "WinError 100"
        };

        static bool IsNetwork(Item it)
        {
            if (string.IsNullOrEmpty(it.Error)) return !NetworkInterface.GetIsNetworkAvailable();
            foreach (string s in NetErrors) if (it.Error.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        // Нет сети: загрузка не падает, а ждёт и повторяет (пауза растёт 5, 10 … 60 с; .part-файлы докачиваются).
        void WaitNetwork(Item it)
        {
            lock (gate)
            {
                it.Tries++;
                int delay = Math.Min(60, 5 * it.Tries);
                it.RetryAt = DateTime.UtcNow.AddSeconds(delay).Ticks;
                it.State = St.Queued; it.Speed = null; it.Eta = null;
                Log.Write("[" + it.Id + "] нет сети (" + it.Error + "), повтор через " + delay + " с, попытка " + it.Tries);
                it.Error = null;
                Persist();
                Pump();
            }
        }

        public Item[] Snapshot()
        {
            if (demoFile != null) foreach (Item d in items) if (d.State == St.Active) d.LastTick = Environment.TickCount;
            lock (gate) return items.ToArray();
        }

        public int Pending()
        {
            lock (gate) return items.Count(i => i.State == St.Active || i.State == St.Queued);
        }

        public Item Add(string videoId, out string msg)
        {
            lock (gate)
            {
                Item dup = items.FirstOrDefault(i => i.VideoId == videoId && i.State != St.Failed);
                if (dup != null)
                {
                    msg = dup.State == St.Done ? "Уже скачано" : "Уже в очереди";
                    return null;
                }
                items.RemoveAll(i => i.VideoId == videoId);
                Item it = new Item
                {
                    Id = Guid.NewGuid().ToString("N").Substring(0, 8),
                    VideoId = videoId,
                    Url = Yt.Url(videoId),
                    State = St.Queued,
                    Quality = cfg.Quality,
                    Container = cfg.Container,
                    Added = DateTime.UtcNow.Ticks
                };
                items.Add(it);
                msg = null;
                Persist();
                Pump();
                return it;
            }
        }

        // Продолжить после паузы / остановки / ошибки (.part-файлы докачиваются).
        public void Resume(Item it)
        {
            lock (gate)
            {
                bool gone = it.State == St.Done && (it.Missing || string.IsNullOrEmpty(it.File) || !File.Exists(it.File));
                if (it.State != St.Failed && it.State != St.Paused && it.State != St.Stopped && !gone) return;
                halt.Remove(it.Id);
                if (gone) { it.Percent = 0; it.Got = 0; it.Bytes = 0; it.File = null; it.Missing = false; }   // скачать заново
                it.State = St.Queued; it.Error = null; it.RetryAt = 0; it.Tries = 0;
                Persist();
                Pump();
            }
        }

        public void ResumeFailed()
        {
            foreach (Item it in Snapshot().Where(i => i.State == St.Failed)) Resume(it);
        }

        public void Pause(Item it) { Halt(it, St.Paused); }

        // Стоп: прерывает загрузку и удаляет недокачанные файлы; в списке остаётся с кнопкой «продолжить».
        public void Stop(Item it) { Halt(it, St.Stopped); }

        void Halt(Item it, St target)
        {
            lock (gate)
            {
                if (it.State == St.Active)
                {
                    halt[it.Id] = target;               // Run применит состояние, когда процесс завершится
                    Process p;
                    if (procs.TryGetValue(it.Id, out p)) Kill(p);
                    return;
                }
                if (it.State != St.Queued && !(it.State == St.Paused && target == St.Stopped)) return;
                it.State = target; it.Speed = null; it.Eta = null;
                if (target == St.Stopped) { it.Percent = 0; CleanPartial(it); }
                Persist();
            }
        }

        public void Remove(Item it, bool withFile = false)
        {
            lock (gate)
            {
                bool wasActive = it.State == St.Active;
                removed.Add(it.Id);
                items.Remove(it);
                Process p;
                if (procs.TryGetValue(it.Id, out p)) Kill(p);
                if (withFile && !string.IsNullOrEmpty(it.File)) DeleteFile(it.File);
                if (it.State != St.Done && !wasActive) CleanPartial(it);
                Persist();
                Pump();
            }
        }

        // Файл может быть на секунду занят антивирусом/проводником — пробуем несколько раз, не блокируя интерфейс.
        static void DeleteFile(string path)
        {
            ThreadPool.QueueUserWorkItem(o =>
            {
                for (int i = 0; i < 20; i++)
                {
                    try { if (File.Exists(path)) File.Delete(path); return; }
                    catch (Exception e)
                    {
                        if (i == 19) Log.Error("удаление файла " + path, e);
                        Thread.Sleep(500);
                    }
                }
            });
        }

        // Удаляет временные файлы недокачанного видео: «… [videoId].f399.mp4.part», «.ytdl» и т.п.
        void CleanPartial(Item it)
        {
            if (string.IsNullOrEmpty(it.VideoId) || !Directory.Exists(cfg.Folder)) return;
            try
            {
                foreach (string f in Directory.GetFiles(cfg.Folder, "* [" + it.VideoId + "]*"))
                    if (f.EndsWith(".part") || f.EndsWith(".ytdl") || f.Contains("].f") || f.EndsWith(".temp.mp4"))
                        File.Delete(f);
            }
            catch (Exception e) { Log.Error("очистка временных файлов", e); }
        }

        public void Start()
        {
            if (demoFile != null) return;
            lock (gate) { started = true; Pump(); }
        }

        public void Shutdown()
        {
            lock (gate)
            {
                closing = true;
                foreach (Process p in procs.Values.ToArray()) Kill(p);
                if (demoFile == null) Store.SaveQueue(items);
            }
        }

        void Persist()
        {
            if (demoFile == null) Store.SaveQueue(items);
            Action c = Changed;
            if (c != null) c();
        }

        void Pump()   // вызывать под gate
        {
            if (!started || closing || !Tools.Ready) return;
            while (items.Count(i => i.State == St.Active) < MaxParallel)
            {
                long now = DateTime.UtcNow.Ticks;
                Item next = items.FirstOrDefault(i => i.State == St.Queued && i.RetryAt <= now);
                if (next == null) break;
                next.State = St.Active; next.Percent = 0; next.Error = null;
                next.Got = 0; next.DoneBase = 0; next.StreamKey = null;
                Item it = next;
                new Thread(() => Run(it)) { IsBackground = true, Name = "dl-" + it.Id }.Start();
            }
            Action c = Changed;
            if (c != null) c();
        }

        static void Kill(Process p)
        {
            try
            {
                ProcessStartInfo k = new ProcessStartInfo("taskkill", "/PID " + p.Id + " /T /F")
                { CreateNoWindow = true, UseShellExecute = false };
                using (Process t = Process.Start(k)) t.WaitForExit(5000);
            }
            catch { try { p.Kill(); } catch { } }
        }

        void Run(Item it)
        {
            bool retried = false;
            int code;
            while (true)
            {
                try { code = Exec(it); }
                catch (Exception e)
                {
                    Log.Error("запуск yt-dlp", e);
                    it.Error = e.Message;
                    code = -2;
                    retried = true;
                }
                if (closing || removed.Contains(it.Id)) return;
                if (ApplyHalt(it)) return;
                if (code != 0 && IsNetwork(it)) { WaitNetwork(it); return; }
                if (code == 0 || retried) break;
                if (it.Error != null && it.Error.Contains("Failed to decrypt"))
                {
                    it.Error = "Chrome/Edge не отдают cookies (шифрование). Выберите Firefox или «Файл cookies.txt»";
                    break;
                }
                if (it.Error != null && it.Error.Contains("Could not copy"))
                {
                    it.Error = "Браузер держит базу cookies: полностью закройте его (и в трее) и нажмите ▶, либо выберите Firefox / cookies.txt";
                    break;
                }
                if (NeedsLogin(it.Error))
                {
                    it.Error = string.IsNullOrEmpty(cfg.Cookies)
                        ? "YouTube требует вход: трей → «Войти в YouTube…»"
                        : "YouTube не принял cookies «" + cfg.Cookies + "»: войдите в YouTube в этом браузере или используйте cookies.txt";
                    break;
                }
                retried = true;                       // частая причина сбоя — устаревший yt-dlp
                Note = "Обновляю yt-dlp…";
                Tools.Update();
                Note = null;
            }
            lock (gate)
            {
                if (code == 0)
                {
                    it.State = St.Done; it.Percent = 100; it.Speed = null; it.Eta = null; it.Error = null;
                    it.Tries = 0; it.RetryAt = 0; it.Missing = false;
                    try { if (!string.IsNullOrEmpty(it.File) && File.Exists(it.File)) { it.Bytes = new FileInfo(it.File).Length; it.Ext = Path.GetExtension(it.File).TrimStart('.').ToUpperInvariant(); } }
                    catch { }
                }
                else { it.State = St.Failed; if (string.IsNullOrEmpty(it.Error)) it.Error = "yt-dlp завершился с кодом " + code; }
                Persist();
                Pump();
            }
            Action<Item> f = Finished;
            if (f != null) f(it);
        }

        static bool NeedsLogin(string err)
        {
            return err != null && (err.Contains("Sign in to confirm") || err.Contains("--cookies"));
        }

        // Если пользователь нажал паузу/стоп — зафиксировать состояние. true = загрузка прервана намеренно.
        bool ApplyHalt(Item it)
        {
            lock (gate)
            {
                St target;
                if (!halt.TryGetValue(it.Id, out target)) return false;
                halt.Remove(it.Id);
                it.State = target; it.Speed = null; it.Eta = null; it.Error = null;
                if (target == St.Stopped) { it.Percent = 0; CleanPartial(it); }
                Persist();
                Pump();
                return true;
            }
        }

        int Exec(Item it)
        {
            ProcessStartInfo psi = new ProcessStartInfo(Tools.YtDlp, Ytdlp.Args(cfg, it))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            psi.EnvironmentVariables["PATH"] = Store.Bin + ";" + Environment.GetEnvironmentVariable("PATH");
            psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            psi.EnvironmentVariables["PYTHONUTF8"] = "1";

            Log.Write("[" + it.Id + "] запуск: " + psi.FileName + " " + psi.Arguments);
            using (Process p = new Process { StartInfo = psi })
            {
                p.OutputDataReceived += (s, e) => Ytdlp.Line(it, e.Data);
                p.ErrorDataReceived += (s, e) => Ytdlp.Line(it, e.Data);
                it.Error = null;
                using (Job job = new Job())
                {
                    lock (gate)
                    {
                        if (closing) return -1;
                        if (halt.ContainsKey(it.Id) || removed.Contains(it.Id)) return -3;
                        p.Start();
                        job.Add(p);                       // yt-dlp + ffmpeg + deno умирают вместе с задачей
                        procs[it.Id] = p;
                    }
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                    p.WaitForExit();
                    job.KillAll();                        // добить потомков: они держат файлы занятыми
                }
                lock (gate) procs.Remove(it.Id);
                Log.Write("[" + it.Id + "] yt-dlp завершён, код " + p.ExitCode);
                return p.ExitCode;
            }
        }
    }
}

