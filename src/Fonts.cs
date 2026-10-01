using System;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace OpenD
{
    // Montserrat (заголовки) и Comfortaa (текст, цифры) зашиты в exe как ресурсы.
    // Если шрифт не загрузился — тихо откатываемся на Segoe UI.
    static class Fonts
    {
        static readonly PrivateFontCollection Pfc = new PrivateFontCollection();
        static FontFamily montserrat, comfortaa;

        public static void Init()
        {
            Assembly asm = Assembly.GetExecutingAssembly();
            foreach (string name in asm.GetManifestResourceNames())
            {
                if (!name.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    using (Stream s = asm.GetManifestResourceStream(name))
                    {
                        byte[] data = new byte[s.Length];
                        int read = 0;
                        while (read < data.Length) read += s.Read(data, read, data.Length - read);
                        IntPtr mem = Marshal.AllocCoTaskMem(data.Length);     // должен жить до конца процесса
                        Marshal.Copy(data, 0, mem, data.Length);
                        Pfc.AddMemoryFont(mem, data.Length);
                    }
                }
                catch (Exception e) { Log.Error("шрифт " + name, e); }
            }
            foreach (FontFamily f in Pfc.Families)
            {
                if (f.Name.StartsWith("Montserrat")) montserrat = montserrat ?? f;
                else if (f.Name.StartsWith("Comfortaa")) comfortaa = comfortaa ?? f;
            }
            Log.Write("шрифты: Montserrat=" + (montserrat != null) + ", Comfortaa=" + (comfortaa != null));
        }

        static Font Make(FontFamily f, float pt, FontStyle st)
        {
            try
            {
                if (f != null && f.IsStyleAvailable(st)) return new Font(f, pt, st, GraphicsUnit.Point);
                if (f != null) return new Font(f, pt, f.IsStyleAvailable(FontStyle.Regular) ? FontStyle.Regular : st, GraphicsUnit.Point);
            }
            catch { }
            return new Font("Segoe UI", pt, st, GraphicsUnit.Point);
        }

        public static Font Title(float pt) { return Make(montserrat, pt, FontStyle.Regular); }
        public static Font Body(float pt) { return Make(comfortaa, pt, FontStyle.Regular); }
        public static Font BodyBold(float pt) { return Make(comfortaa, pt, FontStyle.Bold); }
    }
}
