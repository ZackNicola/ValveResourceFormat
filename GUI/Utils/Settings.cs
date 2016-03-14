﻿using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace GUI.Utils
{
    internal static class Settings
    {
        public static List<string> GameSearchPaths { get; } = new List<string>();

        public static Color BackgroundColor { get; set; } = Color.Black;

        public static void Load()
        {
            // TODO: Be dumb about it for now.
            if (!File.Exists("gamepaths.txt"))
            {
                return;
            }

            GameSearchPaths.AddRange(File.ReadAllLines("gamepaths.txt"));
        }

        public static void Save()
        {
            File.WriteAllLines("gamepaths.txt", GameSearchPaths);
        }
    }
}
