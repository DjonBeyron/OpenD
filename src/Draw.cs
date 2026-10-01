using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace OpenD
{
    // Отрисовка строки очереди. Каждая величина (процент, размер, скорость, время) живёт в своей
    // колонке фиксированной ширины — при изменении чисел соседние элементы не сдвигаются по X.
    static class Draw
    {
        public const int RowUnits = 64;
        const int Slots = 4;

        static readonly StringFormat Ell = new StringFormat(StringFormatFlags.NoWrap | StringFormatFlags.NoClip)
        {
            Trimming = StringTrimming.EllipsisCharacter
        };
        static readonly Color Bright = Color.FromArgb(246, 246, 250);
        static readonly Color Muted = Color.FromArgb(142, 142, 152);
        static Font fTitle, fBody, fBold;

        public static int Slot(float s) { return (int)(28 * s); }
        public static int Reserve(float s) { return Slot(s) * Slots + (int)(12 * s); }

        public static void Setup(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        }

        public static void Row(Graphics g, Item it, Rectangle r, float s)
        {
            if (fTitle == null) { fTitle = Fonts.Title(10f); fBody = Fonts.Body(8.5f); fBold = Fonts.BodyBold(8.5f); }
            int pad = (int)(16 * s);
            int x0 = r.X + pad;
            int w = r.Right - Reserve(s) - x0;
            if (w < 40) return;

            string c1 = "", c2 = "", c3 = "", c4 = "";
            Color k1 = Theme.Dim, titleColor = Theme.Text, k2 = Theme.Dim;
            bool span = false;                               // c2 растягивается на остаток строки (ошибка, ожидание)
            Describe(it, ref c1, ref c2, ref c3, ref c4, ref k1, ref titleColor, ref k2, ref span);

            string title = string.IsNullOrEmpty(it.Title) ? it.Url : it.Title;
            using (SolidBrush tb = new SolidBrush(titleColor))
                g.DrawString(title, fTitle, tb, new RectangleF(x0, r.Y + 10 * s, w, 22 * s), Ell);

            int by = r.Y + (int)(37 * s), bh = Math.Max(2, (int)(3 * s));
            using (SolidBrush bg = new SolidBrush(Theme.Line)) g.FillRectangle(bg, x0, by, w, bh);
            double p = it.State == St.Done ? 100 : it.Percent;
            if (p > 0)
                using (SolidBrush fg = new SolidBrush(BarColor(it)))
                    g.FillRectangle(fg, x0, by, (int)(w * Math.Min(100, p) / 100.0), bh);

            float my = r.Y + 45 * s, mh = 18 * s;
            Cell(g, c1, k1 == Theme.Accent || it.State == St.Done ? fBold : fBody, k1, x0, my, 74 * s, mh);
            float x2 = x0 + 80 * s;
            if (span) { Cell(g, c2, fBody, k2, x2, my, w - 80 * s, mh); return; }
            Cell(g, c2, fBody, k2, x2, my, 122 * s, mh);
            Cell(g, c3, fBody, Theme.Dim, x0 + 208 * s, my, 80 * s, mh);
            Cell(g, c4, fBody, Theme.Dim, x0 + 292 * s, my, 72 * s, mh);
            Tag(g, it, x0 + 372 * s, my, w - 372 * s, mh);
        }

        static void Cell(Graphics g, string text, Font f, Color c, float x, float y, float w, float h)
        {
            if (string.IsNullOrEmpty(text) || w < 8) return;
            using (SolidBrush b = new SolidBrush(c))
                g.DrawString(text, f, b, new RectangleF(x, y, w, h), Ell);
        }

        static Color BarColor(Item it)
        {
            switch (it.State)
            {
                case St.Failed: return Theme.Err;
                case St.Done: return Theme.Ok;
                case St.Active: return Theme.Accent;
                default: return Theme.Dim;
            }
        }

        static void Describe(Item it, ref string c1, ref string c2, ref string c3, ref string c4,
            ref Color k1, ref Color title, ref Color k2, ref bool span)
        {
            string progress = it.Size > 0 ? Fmt.Bytes(it.Got) + " / " + Fmt.Bytes(it.Size) : (it.Got > 0 ? Fmt.Bytes(it.Got) : "");
            switch (it.State)
            {
                case St.Active:
                    bool stalled = it.Got > 0 && Environment.TickCount - it.LastTick > 6000;
                    c2 = progress;
                    if (it.Got <= 0) { c1 = "Старт…"; break; }
                    if (stalled) { c1 = "Ожидание"; k1 = Theme.Warn; c3 = "—"; break; }
                    c1 = (int)it.Percent + "%"; k1 = Theme.Accent;
                    c3 = it.Speed; c4 = it.Eta == null ? "" : "ещё " + it.Eta;
                    break;
                case St.Done:
                    title = Bright;                           // готовые ярче скачиваемых и ожидающих
                    if (it.Missing) { c1 = "Удалён"; c2 = "файла нет на диске"; span = true; title = Muted; break; }
                    c1 = "Готово"; k1 = Theme.Ok;
                    long b = it.Bytes > 0 ? it.Bytes : it.Size;
                    c2 = b > 0 ? Fmt.Bytes(b) : "";
                    break;
                case St.Failed:
                    title = Muted; c1 = "Ошибка"; k1 = Theme.Err; c2 = it.Error ?? ""; span = true; break;
                case St.Paused:
                    title = Muted; c1 = "Пауза"; c2 = progress; break;
                case St.Stopped:
                    title = Muted; c1 = "Остановлено"; break;
                default:
                    title = Muted;
                    if (it.RetryAt > DateTime.UtcNow.Ticks)
                    {
                        c1 = "Нет сети"; k1 = Theme.Warn; span = true;
                        c2 = "повтор через " + (int)Math.Ceiling(new TimeSpan(it.RetryAt - DateTime.UtcNow.Ticks).TotalSeconds) + " с";
                    }
                    else c1 = "В очереди";
                    break;
            }
        }

        // «ОРИГИНАЛ 1080p AV1 WEBM» — метка и две главные характеристики.
        static void Tag(Graphics g, Item it, float x, float y, float w, float h)
        {
            if (w < 70) return;
            string[] parts = (it.Fmt ?? "").Split(new[] { " · " }, StringSplitOptions.RemoveEmptyEntries);
            string rest = string.Join(" ", parts, 0, Math.Min(2, parts.Length));
            if (!string.IsNullOrEmpty(it.Ext)) rest = (rest + " " + it.Ext).Trim();
            if (rest.Length == 0) rest = Presets.Short(Ytdlp.QualityName(it.Quality));
            if (it.Original)
            {
                using (SolidBrush ab = new SolidBrush(Theme.Accent))
                    g.DrawString("ОРИГИНАЛ", fBold, ab, x, y);
                float ow = g.MeasureString("ОРИГИНАЛ", fBold).Width + 2;
                x += ow; w -= ow;
            }
            Cell(g, rest, fBody, Theme.Dim, x, y, w, h);
        }
    }
}
