using System.Reflection;

[assembly: AssemblyTitle("OpenD")]
[assembly: AssemblyProduct("OpenD")]
[assembly: AssemblyDescription("Tray YouTube downloader for Windows")]
[assembly: AssemblyVersion("1.2.0.0")]
[assembly: AssemblyFileVersion("1.2.0.0")]

namespace OpenD
{
    static class AppInfo
    {
        // «1.2.0» — версия из сборки (меняется только в этом файле).
        public static string Version
        {
            get
            {
                System.Version v = Assembly.GetExecutingAssembly().GetName().Version;
                return v.Major + "." + v.Minor + "." + v.Build;
            }
        }
    }
}
