using System;
using System.Globalization;

namespace OpenD
{
    // Форматирование размеров, времени и названий кодеков для интерфейса.
    static class Fmt
    {
        static readonly CultureInfo Ru = new CultureInfo("ru-RU");

        public static string Bytes(long b)
        {
            if (b >= 1L << 30) return (b / (double)(1L << 30)).ToString("0.00", Ru) + " ГБ";
            if (b >= 1L << 20) return (b / (double)(1L << 20)).ToString(b >= 100L << 20 ? "0" : "0.0", Ru) + " МБ";
            if (b >= 1L << 10) return (b >> 10) + " КБ";
            return b + " Б";
        }

        public static string Eta(double sec)
        {
            TimeSpan t = TimeSpan.FromSeconds(sec);
            return t.TotalHours >= 1
                ? string.Format("{0}:{1:00}:{2:00}", (int)t.TotalHours, t.Minutes, t.Seconds)
                : string.Format("{0}:{1:00}", t.Minutes, t.Seconds);
        }

        public static string Codec(string c)
        {
            string k = c.Split('.')[0].ToLowerInvariant();
            switch (k)
            {
                case "vp9": case "vp09": return "VP9";
                case "avc1": case "h264": return "H.264";
                case "av01": case "av1": return "AV1";
                case "hev1": case "hvc1": return "HEVC";
                case "opus": return "Opus";
                case "mp4a": return "AAC";
                case "ec-3": return "E-AC3";
                default: return k.ToUpperInvariant();
            }
        }
    }
}
