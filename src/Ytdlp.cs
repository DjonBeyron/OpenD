using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace OpenD
{
    // Всё, что касается командной строки yt-dlp и разбора его вывода.
    static class Ytdlp
    {
        public static readonly string[][] Qualities =
        {
            new[] { "best", "Оригинал (лучшее качество)" },
            new[] { "2160", "2160p (4K)" }, new[] { "1440", "1440p" }, new[] { "1080", "1080p" },
            new[] { "720", "720p" }, new[] { "480", "480p" },
            new[] { "mp4", "MP4 H.264 (совместимость)" },
            new[] { "audio", "Только звук (оригинал)" }, new[] { "mp3", "Только звук MP3" }
        };

        public static readonly string[][] Containers =
        {
            new[] { "auto", "Авто (без перекодирования)" }, new[] { "mp4", "MP4" },
            new[] { "mkv", "MKV" }, new[] { "webm", "WebM" }
        };

        public static string QualityName(string key)
        {
            foreach (string[] q in Qualities) if (q[0] == key) return q[1];
            return Qualities[0][1];
        }

        public static string ContainerName(string key)
        {
            foreach (string[] c in Containers) if (c[0] == key) return c[1];
            return Containers[0][1];
        }

        static string Q(string s) { return "\"" + s.Replace("\"", "\\\"") + "\""; }

        public static string Args(Settings cfg, Item it)
        {
            string folder = cfg.Folder;
            Directory.CreateDirectory(folder);
            string q = it.Quality ?? "best", c = it.Container ?? "auto";
            StringBuilder a = new StringBuilder();
            a.Append("--ignore-config --force-ipv4 --encoding utf-8 --no-playlist --quiet --progress --newline ");
            a.Append("--windows-filenames --no-mtime --retries infinite --fragment-retries infinite ");
            a.Append("--concurrent-fragments 4 --embed-metadata --socket-timeout 20 --extractor-retries 3 ");
            a.Append("--retry-sleep http:exp=1:30 --retry-sleep fragment:exp=1:30 ");
            if (!string.IsNullOrEmpty(cfg.Proxy)) a.Append("--proxy ").Append(Q(cfg.Proxy)).Append(' ');
            switch (q)
            {
                case "mp4": a.Append("-S vcodec:h264,res,acodec:m4a --merge-output-format mp4 "); break;
                case "audio": a.Append("-f ba/b -x "); break;
                case "mp3": a.Append("-f ba/b -x --audio-format mp3 --audio-quality 0 "); break;
                case "2160": case "1440": case "1080": case "720": case "480":
                    a.Append("-f ").Append(Q("bv*[height<=" + q + "]+ba/b[height<=" + q + "]/bv*+ba/b")).Append(' ');
                    break;
                default: a.Append("-f bv*+ba/b "); break;
            }
            if (c != "auto" && q != "mp4" && q != "audio" && q != "mp3") a.Append("--merge-output-format ").Append(c).Append(' ');
            if (cfg.Cookies == "file" && File.Exists(Path.Combine(Store.Dir, "cookies.txt")))
                a.Append("--cookies ").Append(Q(Path.Combine(Store.Dir, "cookies.txt"))).Append(' ');
            else if (!string.IsNullOrEmpty(cfg.Cookies) && cfg.Cookies != "file")
                a.Append("--cookies-from-browser ").Append(cfg.Cookies).Append(' ');
            a.Append("--ffmpeg-location ").Append(Q(Store.Bin)).Append(' ');
            a.Append("--progress-template ").Append(Q("download:OPD|%(info.vcodec)s|%(info.acodec)s|" +
                "%(progress.downloaded_bytes)s|%(progress.total_bytes)s|%(progress.total_bytes_estimate)s|" +
                "%(progress.speed)s|%(progress.eta)s")).Append(' ');
            a.Append("--print ").Append(Q("before_dl:OPD_TITLE|%(title)s")).Append(' ');
            a.Append("--print ").Append(Q("before_dl:OPD_FMT|%(height)s|%(fps)s|%(vcodec)s|%(acodec)s|%(ext)s|%(filesize,filesize_approx)s")).Append(' ');
            a.Append("--print ").Append(Q("after_move:OPD_FILE|%(filepath)s")).Append(' ');
            a.Append("-o ").Append(Q(Path.Combine(folder, "%(title).120B [%(id)s].%(ext)s"))).Append(' ');
            a.Append(Q(it.Url));
            return a.ToString();
        }

        static double D(string s)
        {
            double v;
            return double.TryParse((s ?? "").Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0;
        }

        static bool Has(string codec) { return !string.IsNullOrEmpty(codec) && codec != "none" && codec != "NA" && codec != "None"; }

        public static void Line(Item it, string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            if (!line.StartsWith("OPD|")) Log.Write("[" + it.Id + "] " + line);
            if (line.StartsWith("OPD_TITLE|")) { it.Title = line.Substring(10); return; }
            if (line.StartsWith("OPD_FILE|")) { it.File = line.Substring(9); return; }
            if (line.StartsWith("OPD_FMT|")) { Format(it, line.Substring(8).Split('|')); return; }
            if (line.StartsWith("OPD|")) { Progress(it, line.Split('|')); return; }
            if (line.StartsWith("ERROR:")) it.Error = line.Substring(6).Trim();
            else if (line.Contains("ookie") && line.Contains("Chrome") && line.Contains("Could not")) it.Error = line.Trim();
        }

        static void Format(Item it, string[] p)
        {
            if (p.Length < 6) return;
            StringBuilder s = new StringBuilder();
            double h = D(p[0]), fps = D(p[1]);
            if (h > 0) s.Append((int)h).Append('p').Append(fps > 30 ? ((int)Math.Round(fps)).ToString() : "");
            if (Has(p[2])) Add(s, Fmt.Codec(p[2]));
            if (Has(p[3])) Add(s, Fmt.Codec(p[3]));
            it.Fmt = s.ToString();
            it.Ext = Has(p[4]) ? p[4].ToUpperInvariant() : null;
            it.Size = (long)D(p[5]);
            it.Original = it.Quality == null || it.Quality == "best" || it.Quality == "audio";
        }

        static void Add(StringBuilder s, string part)
        {
            if (s.Length > 0) s.Append(" · ");
            s.Append(part);
        }

        static void Progress(Item it, string[] p)
        {
            if (p.Length < 8) return;
            bool hasV = Has(p[1]), hasA = Has(p[2]);
            string key = p[1] + "|" + p[2];
            double dl = D(p[3]), tot = D(p[4]);
            if (tot <= 0) tot = D(p[5]);
            if (it.StreamKey != null && it.StreamKey != key) it.DoneBase += it.LastTot > 0 ? it.LastTot : it.LastDl;
            it.StreamKey = key; it.LastDl = dl; it.LastTot = tot; it.LastTick = Environment.TickCount;
            it.Got = (long)(it.DoneBase + dl);
            it.Stage = hasV && hasA ? "Загрузка" : hasV ? "Видео" : "Аудио";
            double pct;
            if (it.Size > 0) pct = it.Got * 100.0 / it.Size;
            else
            {
                double sp = tot > 0 ? dl * 100.0 / tot : 0;
                pct = hasV && hasA ? sp : hasV ? sp * 0.92 : 92 + sp * 0.08;
            }
            it.Percent = Math.Max(it.Percent, Math.Min(99, pct));
            double bps = D(p[6]), eta = D(p[7]);
            it.Speed = bps > 0 ? Fmt.Bytes((long)bps) + "/с" : null;
            it.Eta = eta > 0 ? Fmt.Eta(eta) : null;
        }
    }
}
