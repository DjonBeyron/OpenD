using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;

namespace OpenD
{
    // Отрисовка строки загрузки — общая для мини-HUD и окна очереди.
    static class Draw
    {
        static readonly StringFormat Ell = new StringFormat(StringFormatFlags.NoWrap)
        {
            Trimming = StringTrimming.EllipsisCharacter
        };
        static readonly StringFormat Right = new StringFormat(StringFormatFlags.NoWrap)
        {
            Alignment = StringAlignment.Far
        };
        static readonly Color Bright = Color.FromArgb(246, 246, 250);
        static readonly Color Muted = Color.FromArgb(150, 150, 160);

        public static void Setup(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        }

        // full = добавить строку с форматом («ОРИГИНАЛ · 2160p · AV1 · Opus · MKV»); reserve — место под кнопки справа.
        public static void Row(Graphics g, Item it, Rectangle r, float s, Font f, Font small, int reserve = 0, bool full = false)
        {
            int pad = (int)(14 * s);
            int w = r.Width - pad * 2;
            int statusW = (int)(104 * s);
            string title = string.IsNullOrEmpty(it.Title) ? it.Url : it.Title;
            string status; Color sc; Color tc = Theme.Text;
            string detail = Detail(it, out status, out sc, ref tc);

            using (SolidBrush tb = new SolidBrush(tc))
            using (SolidBrush sb = new SolidBrush(sc))
            using (SolidBrush db = new SolidBrush(it.State == St.Active ? Theme.Text : Theme.Dim))
            {
                g.DrawString(title, f, tb, new RectangleF(r.X + pad, r.Y, w - statusW, 20 * s), Ell);
                g.DrawString(status, small, sb, new RectangleF(r.Right - pad - statusW, r.Y + 2 * s, statusW, 18 * s), Right);
                g.DrawString(detail, small, db, new RectangleF(r.X + pad, r.Y + 28 * s, w - reserve, 16 * s), Ell);
            }
            int by = r.Y + (int)(23 * s), bh = Math.Max(2, (int)(3 * s));
            using (SolidBrush bg = new SolidBrush(Theme.Line))
                g.FillRectangle(bg, r.X + pad, by, w, bh);
            double p = it.State == St.Done ? 100 : it.Percent;
            if (p > 0)
                using (SolidBrush fg = new SolidBrush(it.State == St.Failed ? Theme.Err : it.State == St.Done ? Theme.Ok : it.State == St.Active ? Theme.Accent : Theme.Dim))
                    g.FillRectangle(fg, r.X + pad, by, (int)(w * Math.Min(100, p) / 100.0), bh);
            if (full) Tag(g, it, new RectangleF(r.X + pad, r.Y + 48 * s, w - reserve, 16 * s), small);
        }

        static string Detail(Item it, out string status, out Color sc, ref Color tc)
        {
            string progress = it.Size > 0 ? Fmt.Bytes(it.Got) + " / " + Fmt.Bytes(it.Size) : Fmt.Bytes(it.Got);
            switch (it.State)
            {
                case St.Active:
                    status = (it.Stage ?? "Загрузка") + " " + (int)it.Percent + "%"; sc = Theme.Accent;
                    return Join(Join(it.Got > 0 ? progress : null, it.Speed), it.Eta == null ? null : "ещё " + it.Eta);
                case St.Done:
                    status = "Готово"; sc = Theme.Ok; tc = Bright;       // готовые ярче скачиваемых и ожидающих
                    long b = it.Bytes > 0 ? it.Bytes : it.Size;
                    return b > 0 ? Fmt.Bytes(b) : "Сохранено";
                case St.Failed:
                    status = "Ошибка"; sc = Theme.Err; tc = Muted; return it.Error ?? "";
                case St.Paused:
                    status = "Пауза " + (int)it.Percent + "%"; sc = Theme.Dim; tc = Muted;
                    return Join("Приостановлено", it.Got > 0 ? progress : null);
                case St.Stopped:
                    status = "Остановлено"; sc = Theme.Dim; tc = Muted; return "";
                default:
                    status = "В очереди"; sc = Theme.Dim; tc = Muted; return "";
            }
        }

        static void Tag(Graphics g, Item it, RectangleF rect, Font small)
        {
            string ext = it.Ext;
            string rest = Join(it.Fmt, ext);
            if (string.IsNullOrEmpty(rest)) rest = Ytdlp.QualityName(it.Quality);
            float x = rect.X;
            if (it.Original)
                using (Font bold = new Font(small, FontStyle.Bold))
                using (SolidBrush ab = new SolidBrush(Theme.Accent))
                {
                    g.DrawString("ОРИГИНАЛ", bold, ab, x, rect.Y);
                    x += g.MeasureString("ОРИГИНАЛ", bold).Width - 4;
                    rest = " · " + rest;
                }
            using (SolidBrush db = new SolidBrush(Theme.Dim))
                g.DrawString(rest, small, db, new RectangleF(x, rect.Y, Math.Max(10, rect.Right - x), rect.Height), Ell);
        }

        static string Join(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return b ?? "";
            return string.IsNullOrEmpty(b) ? a : a + " · " + b;
        }
    }
}
