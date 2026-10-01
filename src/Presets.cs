using System;
using System.Windows.Forms;

namespace OpenD
{
    // Меню выбора качества и контейнера — общее для трея и окна.
    static class Presets
    {
        public static void Fill(ToolStripItemCollection dst, string[][] opts, string current, Action<string> set)
        {
            foreach (string[] o in opts)
            {
                string key = o[0];
                ToolStripMenuItem it = new ToolStripMenuItem(o[1]) { ForeColor = Theme.Text, Checked = key == current };
                it.Click += (s, e) => set(key);
                dst.Add(it);
            }
        }

        // Подменю для трея: пункты пересобираются при каждом открытии (актуальная галочка).
        public static ToolStripMenuItem Submenu(string title, string[][] opts, Func<string> current, Action<string> set)
        {
            ToolStripMenuItem root = new ToolStripMenuItem(title) { ForeColor = Theme.Text };
            ToolStripDropDownMenu dd = (ToolStripDropDownMenu)root.DropDown;
            dd.BackColor = Theme.Panel;
            dd.ForeColor = Theme.Text;
            dd.ShowImageMargin = false;
            dd.Renderer = new ToolStripProfessionalRenderer(new DarkColors());
            root.DropDownItems.Add(new ToolStripMenuItem("…"));
            root.DropDownOpening += (s, e) =>
            {
                root.DropDownItems.Clear();
                Fill(root.DropDownItems, opts, current(), set);
            };
            return root;
        }

        public static string Short(string name)
        {
            int i = name.IndexOf('(');
            return (i > 0 ? name.Substring(0, i) : name).Trim();
        }
    }
}
