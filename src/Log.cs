using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace OpenD
{
    // Журнал отладки: %APPDATA%\OpenD\log.txt (при переполнении старый уходит в log.old.txt).
    static class Log
    {
        public static readonly string Path = System.IO.Path.Combine(Store.Dir, "log.txt");
        static readonly object Gate = new object();

        public static void Write(string text)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(Store.Dir);
                    FileInfo fi = new FileInfo(Path);
                    if (fi.Exists && fi.Length > 1024 * 1024)
                    {
                        string old = System.IO.Path.Combine(Store.Dir, "log.old.txt");
                        if (File.Exists(old)) File.Delete(old);
                        File.Move(Path, old);
                    }
                    File.AppendAllText(Path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + text + Environment.NewLine,
                        new UTF8Encoding(false));
                }
            }
            catch { }
        }

        public static void Error(string where, Exception e)
        {
            Write("ОШИБКА [" + where + "] " + e);
        }

        public static void Open()
        {
            try
            {
                if (!File.Exists(Path)) Write("лог создан");
                Process.Start("notepad.exe", "\"" + Path + "\"");
            }
            catch { }
        }
    }
}
