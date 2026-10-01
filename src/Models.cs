using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace OpenD
{
    public enum St { Queued = 0, Active = 1, Done = 2, Failed = 3, Paused = 4, Stopped = 5 }

    public class Item
    {
        public string Id;
        public string Url;
        public string VideoId;
        public string Title;
        public int Status;
        public double Percent;
        public string Speed;
        public string Eta;
        public string Stage;
        public string File;
        public string Error;
        public long Added;
        public string Quality, Container;   // пресет качества и контейнер на момент добавления
        public string Fmt, Ext;             // «2160p60 · AV1 · Opus», «MKV»
        public bool Original;               // скачаны лучшие потоки без перекодирования
        public long Size;                   // ожидаемый размер, байт
        public long Bytes;                  // размер готового файла
        public long Got;                    // скачано, байт
        public double DoneBase, LastDl, LastTot;
        public string StreamKey;

        public St State { get { return (St)Status; } set { Status = (int)value; } }
    }

    public class Settings
    {
        public string Folder;
        public uint Mods = 0x0003;      // Ctrl+Alt
        public int Key = 0x44;          // D
        public bool WatchClipboard;
        public bool PreferMp4;          // устарело: перенесено в Quality
        public string Quality = "best";
        public string Container = "auto";
        public bool StartInTray = true; // false — при запуске показывать окно
        public string Cookies = "";   // "", "firefox", "edge", "chrome", "brave" или "file" (cookies.txt рядом с настройками)
        public long LastUpdate;
        public bool Seen;

        public string HotkeyText()
        {
            string s = "";
            if ((Mods & 2) != 0) s += "Ctrl+";
            if ((Mods & 1) != 0) s += "Alt+";
            if ((Mods & 4) != 0) s += "Shift+";
            if ((Mods & 8) != 0) s += "Win+";
            return s + ((System.Windows.Forms.Keys)Key).ToString();
        }
    }

    public static class Yt
    {
        static readonly Regex Rx = new Regex(
            @"https?://(?:www\.|m\.|music\.)?(?:youtube\.com/(?:watch\?(?:[^\s#]*&)?v=|shorts/|live/|embed/)|youtu\.be/)([\w-]{11})",
            RegexOptions.IgnoreCase);

        public static string Id(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            Match m = Rx.Match(text);
            return m.Success ? m.Groups[1].Value : null;
        }

        public static string Url(string id) { return "https://www.youtube.com/watch?v=" + id; }
    }

    public static class Store
    {
        public static readonly string Dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OpenD");
        public static readonly string Bin = Path.Combine(Dir, "bin");
        static readonly string QueueFile = Path.Combine(Dir, "queue.json");
        static readonly string SettingsFile = Path.Combine(Dir, "settings.json");
        static readonly JavaScriptSerializer Js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        static readonly object IoLock = new object();

        public static string DefaultFolder()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "OpenD");
        }

        static void WriteAtomic(string path, string text)
        {
            lock (IoLock)
            {
                Directory.CreateDirectory(Dir);
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, text, new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(tmp, path, path + ".bak");
                else File.Move(tmp, path);
            }
        }

        static T Read<T>(string path) where T : class
        {
            foreach (string p in new[] { path, path + ".bak" })
            {
                try
                {
                    if (File.Exists(p)) return Js.Deserialize<T>(File.ReadAllText(p, Encoding.UTF8));
                }
                catch { }
            }
            return null;
        }

        public static Settings LoadSettings()
        {
            Settings s = Read<Settings>(SettingsFile) ?? new Settings();
            if (string.IsNullOrEmpty(s.Folder)) s.Folder = DefaultFolder();
            if (s.PreferMp4) { s.Quality = "mp4"; s.PreferMp4 = false; }
            if (string.IsNullOrEmpty(s.Quality)) s.Quality = "best";
            if (string.IsNullOrEmpty(s.Container)) s.Container = "auto";
            return s;
        }

        public static bool SettingsExist() { return File.Exists(SettingsFile); }

        public static void SaveSettings(Settings s)
        {
            try { WriteAtomic(SettingsFile, Js.Serialize(s)); } catch { }
        }

        public static List<Item> LoadQueue()
        {
            List<Item> l = Read<List<Item>>(QueueFile);
            return l ?? new List<Item>();
        }

        public static void SaveQueue(IEnumerable<Item> items)
        {
            try { WriteAtomic(QueueFile, Js.Serialize(new List<Item>(items))); } catch { }
        }
    }
}
