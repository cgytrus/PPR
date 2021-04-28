﻿using System.IO;

using MoonSharp.Interpreter;

using PPR.GUI;
using PPR.LuaConsole.Rendering;
using PPR.Main;
using PPR.Main.Levels;

namespace PPR.LuaConsole.Main {
    public class Game : Scripts.Main.Game {
        public static string statsState => PPR.Main.Game.statsState switch {
            StatsState.Fail => "fail",
            StatsState.Pass => "pass",
            _ => "pause"
        };

        public static bool editing {
            get => PPR.Main.Game.editing;
            set => PPR.Main.Game.editing = value;
        }
        
        public static bool auto {
            get => PPR.Main.Game.auto;
            set => PPR.Main.Game.auto = value;
        }

        public static bool playing {
            get => PPR.Main.Game.playing;
            set => PPR.Main.Game.playing = value;
        }

        public static bool changed => PPR.Main.Game.changed;

        public static float exitTime {
            get => PPR.Main.Game.exitTime;
            set => PPR.Main.Game.exitTime = value;
        }
        
        public static void Exit() => PPR.Main.Game.Exit();

        public static void GenerateLevelList() => PPR.Main.Game.GenerateLevelList();

        public static void LoadLevel(string levelName, string diffName) {
            string path = Path.Join("levels", levelName);
            Map.LoadLevelFromPath(path, levelName, diffName);
            PPR.Main.Game.RecalculatePosition();
        }

        public static void SaveLevel(string levelName, string diffName) {
            PPR.Main.Game.changed = false;
            string path = Path.Join("levels", levelName);
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Join(path, $"{diffName}.txt"), Map.TextFromLevel(Map.currentLevel));
        }

        public static void SubscribeEvent(object caller, string name, Closure closure) =>
            Lua.SubscribeEvent(caller, name, closure);
    }
}
