using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace OpenD
{
    // Montserrat (Regular, Medium, SemiBold) зашит в exe как ресурсы и используется везде.
    // Шрифты регистрируются и для GDI+ (самодельные контролы), и для GDI (меню, кнопки) —
    // только на время работы процесса. Если не загрузились — тихо откатываемся на Segoe UI.
    static class Fonts
    {
        [DllImport("gdi32.dll")]
        static extern IntPtr AddFontMemResourceEx(IntPtr pbFont, uint cbFont, IntPtr pdv, [In] ref uint pcFonts);

        static readonly PrivateFontCollection Pfc = new PrivateFontCollection();
        static readonly Dictionary<string, FontFamily> Fam = new Dictionary<string, FontFamily>();

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
                        uint n = 0;
                        AddFontMemResourceEx(mem, (uint)data.Length, IntPtr.Zero, ref n);
                    }
                }
                catch (Exception e) { Log.Error("шрифт " + name, e); }
            }
            foreach (FontFamily f in Pfc.Families) Fam[f.Name] = f;
            Log.Write("шрифты: " + string.Join(", ", new List<string>(Fam.Keys).ToArray()));
        }

        static Font Make(string family, float pt)
        {
            try
            {
                FontFamily f;
                if (Fam.TryGetValue(family, out f)) return new Font(f, pt, FontStyle.Regular, GraphicsUnit.Point);
            }
            catch { }
            return new Font("Segoe UI", pt, FontStyle.Regular, GraphicsUnit.Point);
        }

        public static Font Title(float pt) { return Make("Montserrat Medium", pt); }
        public static Font Body(float pt) { return Make("Montserrat", pt); }
        public static Font BodyBold(float pt) { return Make("Montserrat SemiBold", pt); }

        // Для меню и обычных контролов (GDI): берём системно зарегистрированный Montserrat, если он доступен.
        public static Font Gdi(float pt)
        {
            Font f = new Font("Montserrat", pt, FontStyle.Regular, GraphicsUnit.Point);
            if (f.Name == "Montserrat") return f;
            f.Dispose();
            return new Font("Segoe UI", pt, FontStyle.Regular, GraphicsUnit.Point);
        }
    }
}
