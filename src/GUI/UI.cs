﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using PPR.GUI.Elements;
using PPR.Main;
using PPR.Main.Levels;
using PPR.Main.Managers;
using PPR.Properties;

using PRR;

using SFML.Audio;
using SFML.Graphics;
using SFML.System;
using SFML.Window;

namespace PPR.GUI {
    public static class UI {
        public static int fps = 0;
        public static int avgFPS = 0;
        public static int tempAvgFPS = 0;
        public static int tempAvgFPSCounter = 0;
        public static int tps = 0;

        private static readonly Random random = new Random();
        private static readonly Perlin perlin = new Perlin();

        private static readonly Color[] prevHealthColors = new Color[80];
        private static readonly Color[] healthColors = new Color[80];
        private static readonly float[] healthAnimTimes = new float[80];
        private static readonly float[] healthAnimRateOffsets = new float[80];
        public static int health {
            set {
                for(int x = 0; x < 80; x++) {
                    Color color = value > x ? ColorScheme.GetColor("health_bar") : ColorScheme.GetColor("dark_health_bar");
                    if(healthColors[x] != color) {
                        prevHealthColors[x] = healthColors[x];
                        healthAnimTimes[x] = 0f;
                        healthAnimRateOffsets[x] = random.NextFloat(-3f, 3f);
                    }
                    healthColors[x] = color;
                }
            }
        }

        private static readonly Color[] prevProgressColors = new Color[80];
        private static readonly Color[] progressColors = new Color[80];
        private static readonly float[] progressAnimTimes = new float[80];
        private static readonly float[] progressAnimRateOffsets = new float[80];
        public static int progress {
            set {
                for(int x = 0; x < 80; x++) {
                    Color color = value > x ? ColorScheme.GetColor("progress_bar") : ColorScheme.GetColor("dark_progress_bar");
                    if(progressColors[x] != color) {
                        prevProgressColors[x] = progressColors[x];
                        progressAnimTimes[x] = 0f;
                        progressAnimRateOffsets[x] = random.NextFloat(-3f, 3f);
                    }
                    progressColors[x] = color;
                }
            }
        }

        public static Color currentBackground {
            get => levelBackground ?? ColorScheme.GetColor("background");
            set => levelBackground = value;
        }
        public static Color? levelBackground;
        
        public static string currSelectedLevel;
        public static string currSelectedDiff;
        public static Dictionary<string, LevelSelectLevel> levelSelectLevels;
        
        public static Slider musicVolumeSlider;
        public static Slider soundsVolumeSlider;
        
        public static Button bloomSwitch;
        public static Button fullscreenSwitch;
        public static Slider fpsLimitSlider;
        public static Button uppercaseSwitch;
        
        public static Button showFpsSwitch;

        public static int menusAnimBPM = 120;
        public static IReadOnlyDictionary<Vector2i, float> positionRandoms => _positionRandoms;
        public static bool fadeInFinished { get; private set; }
        public static bool fadeOutFinished { get; private set; }

        public static Vector2i prevMousePosition { get; private set; }

        private static readonly string[] mainMenuText = File.ReadAllLines(Path.Join("resources", "ui", "mainMenu.txt"));
        private static readonly string[] settingsText = File.ReadAllLines(Path.Join("resources", "ui", "settings.txt"));
        private static readonly string[] keybindsEditorText = File.ReadAllLines(Path.Join("resources", "ui", "keybinds.txt"));
        private static readonly string[] levelSelectText = File.ReadAllLines(Path.Join("resources", "ui", "levelSelect.txt"));

        private static readonly string[] lastStatsText = File.ReadAllLines(Path.Join("resources", "ui", "lastStats.txt"));
        //static readonly string[] notificationsText = File.ReadAllLines(Path.Join("resources", "ui", "notifications.txt"));
        private static List<Button> _mainMenuButtons;

        private static List<Button> _levelSelectButtons;

        private static string _lastLevel = "";
        private static string _lastDiff = "";
        private static List<Button> _gameLastStatsButtons;
        private static List<Button> _editorLastStatsButtons;

        private static List<Button> _levelEditorButtons;

        private static Button _skipButton;

        //static bool _showNotificationsMenu;

        private static readonly Vector2i zero = new Vector2i();
        public static void RecreateButtons() {
            const Renderer.Alignment center = Renderer.Alignment.Center;
            const Renderer.Alignment right = Renderer.Alignment.Right;
            _mainMenuButtons = new List<Button> {
                new Button(new Vector2i(40, 25), "PLAY", "mainMenu.play", 4, new InputKey("Enter"), center),
                new Button(new Vector2i(40, 27), "EDIT", "mainMenu.edit", 4, new InputKey("LShift,RShift"), center),
                new Button(new Vector2i(40, 29), "SETTINGS", "mainMenu.settings", 8, new InputKey("Tab"), center),
                new Button(new Vector2i(40, 31), "EXIT", "mainMenu.exit", 4, new InputKey("Tilde"), center),
                new Button(new Vector2i(1, 1), "SFML", "mainMenu.sfml", 4),
                new Button(new Vector2i(6, 1), "GITHUB", "mainMenu.github", 6),
                new Button(new Vector2i(13, 1), "DISCORD", "mainMenu.discord", 7)
            };
            _pauseMusicButton = new Button(new Vector2i(1, 58), "►", "mainMenu.music.pause", 1,
                new InputKey("Space"));
            _switchMusicButton = new Button(new Vector2i(3, 58), "»", "mainMenu.music.switch", 1,
                new InputKey("Right"));
            _levelSelectButtons = new List<Button> {
                new Button(new Vector2i(25, 10), "AUTO", "levelSelect.auto", 4, new InputKey("Tab")),
                new Button(new Vector2i(39, 52), "BACK", "levelSelect.back", 4, new InputKey("Escape"), center)
            };
            _gameLastStatsButtons = new List<Button> {
                new Button(new Vector2i(2, 53), "CONTINUE", "lastStats.continue", 8, new InputKey("Enter")),
                new Button(new Vector2i(2, 55), "RESTART", "lastStats.restart", 7, new InputKey("LControl+R,RControl+R")),
                new Button(new Vector2i(10, 55), "AUTO", "lastStats.auto", 4, new InputKey("Tab")),
                new Button(new Vector2i(2, 57), "EXIT", "lastStats.exit", 4, new InputKey("Tilde"))
            };
            _editorLastStatsButtons = new List<Button> {
                new Button(new Vector2i(2, 51), "CONTINUE", "lastStats.continue", 8, new InputKey("Enter")),
                new Button(new Vector2i(2, 53), "SAVE", "lastStats.save", 4, new InputKey("LControl+S,RControl+S")),
                new Button(new Vector2i(2, 55), "SAVE & EXIT", "lastStats.saveAndExit", 11),
                new Button(new Vector2i(2, 57), "EXIT", "lastStats.exit", 4, new InputKey("Tilde"))
            };
            _levelEditorButtons = new List<Button> {
                new Button(new Vector2i(78, 58), "►", "editor.playPause", 1, new InputKey("Space")),
                new Button(hpDrainPos, "<", "editor.hp.drain.down", 1),
                new Button(hpDrainPos + new Vector2i(2, 0), ">", "editor.hp.drain.up", 1),
                new Button(hpRestoragePos, "<", "editor.hp.restorage.down", 1),
                new Button(hpRestoragePos + new Vector2i(2, 0), ">", "editor.hp.restorage.up", 1),
                new Button(musicOffsetPos, "<", "editor.music.offset.down", 1),
                new Button(musicOffsetPos + new Vector2i(2, 0), ">", "editor.music.offset.up", 1)
            };

            //_notificationsMenuButton = new Button(new Vector2i(78, 1), "□", "mainMenu.notifications", 1);
            
            _musicSpeedSlider = new Slider(new Vector2i(78, 58), 25, 100, 16, 100,
                "[value]%", "", "editor.music.speed", Renderer.Alignment.Right);
            _skipButton = new Button(new Vector2i(78, 58), "SKIP", "game.skip", 4,
                new InputKey("Space"), right);

            musicVolumeSlider = new Slider(new Vector2i(), 0, 100, 21, 15, "MUSIC VOLUME",
                "[value]", "settings.volume.music");
            soundsVolumeSlider = new Slider(new Vector2i(), 0, 100, 21, 10, "SOUNDS VOLUME",
                "[value]", "settings.volume.sounds");

            bloomSwitch = new Button(new Vector2i(4, 24), "BLOOM", "settings.bloom", 5);
            fullscreenSwitch = new Button(new Vector2i(4, 26), "FULLSCREEN", "settings.fullscreen", 10);
            fpsLimitSlider = new Slider(new Vector2i(4, 28), 0, 1020, 18, 480, "FPS LIMIT", "[value]",
                "settings.fpsLimit");
            uppercaseSwitch = new Button(new Vector2i(4, 30), "UPPERCASE NOTES", "settings.uppercaseNotes", 15);

            showFpsSwitch = new Button(new Vector2i(4, 39), "SHOW FPS", "settings.showFPS", 8);

            //_keybindsButton = new Button(new Vector2i(2, 57), "KEYBINDS", "settings.keybinds", 8);

            UpdateAllFolderSwitchButtons();
        }
        
        private static Dictionary<Vector2i, float> _positionRandoms;
        public static void RegenPositionRandoms() {
            _positionRandoms = new Dictionary<Vector2i, float>(Core.renderer.width * Core.renderer.height);
            for(int x = 0; x < Core.renderer.width; x++)
                for(int y = 0; y < Core.renderer.height; y++)
                    _positionRandoms[new Vector2i(x, y)] = random.NextFloat(0f, 1f);
        }
        
        public static void UpdateAnims() {
            bool useScriptCharMod =
                Scripts.Rendering.Renderer.scriptCharactersModifier != null && Game.menu == Menu.Game;
            if(useScriptCharMod) Core.renderer.charactersModifier = Scripts.Rendering.Renderer.scriptCharactersModifier;
            
            levelBackground = Game.menu == Menu.Game ?
                Scripts.Rendering.Renderer.scriptBackgroundModifier?.Invoke(ColorScheme.GetColor("background")) : null;
            
            if(fadeInFinished && fadeOutFinished) { if(!useScriptCharMod) Core.renderer.charactersModifier = null; }
            else {
                fadeInFinished = true;
                fadeOutFinished = true;
            }
        }

        public static bool LineSegmentIntersection(Vector2i a1, Vector2i a2, Vector2i b1, Vector2i b2) {
            int o1 = Orientation(a1, a2, b1);
            int o2 = Orientation(a1, a2, b2);
            int o3 = Orientation(b1, b2, a1);
            int o4 = Orientation(b1, b2, a2);

            return o1 != o2 && o3 != o4 ||
                   o1 == 0 && OnSegment(a1, b1, a2) || o2 == 0 && OnSegment(a1, b2, a2) ||
                   o3 == 0 && OnSegment(b1, a1, b2) || o4 == 0 && OnSegment(b1, a2, b2);
        }
        private static bool OnSegment(Vector2i p, Vector2i q, Vector2i r) =>
            q.X <= Math.Max(p.X, r.X) &&
            q.X >= Math.Min(p.X, r.X) &&
            q.Y <= Math.Max(p.Y, r.Y) &&
            q.Y >= Math.Min(p.Y, r.Y);
        private static int Orientation(Vector2i p, Vector2i q, Vector2i r) {
            float val = (q.Y - p.Y) * (r.X - q.X) - (q.X - p.X) * (r.Y - q.Y);
            if(val == 0) return 0;
            return val > 0 ? 1 : 2;
        }
        
        private static Button _pauseMusicButton;
        private static Button _switchMusicButton;
        private static readonly Vector2i nowPlayingCtrlPos = new Vector2i(5, 58);
        private static readonly Vector2i nowPlayingPos = new Vector2i(1, 58);
        private static void DrawNowPlaying(bool controls = false) {
            string text = $"NOW PLAYING : {SoundManager.currentMusicName}";
            Core.renderer.DrawText(controls ? nowPlayingCtrlPos : nowPlayingPos, text);
            if(!controls) return;
            _pauseMusicButton.text = SoundManager.music.Status switch
            {
                SoundStatus.Playing => "║",
                _ => "►"
            };
            if(_pauseMusicButton.Draw())
                switch(_pauseMusicButton.text) {
                    case "►": SoundManager.music.Play();
                        break;
                    case "║": SoundManager.music.Pause();
                        break;
                }

            if(_switchMusicButton.Draw()) SoundManager.SwitchMusic();
        }

        private static readonly Clock fadeInClock = new Clock();
        private static readonly Clock fadeOutClock = new Clock();
        public static void FadeIn(float speed = 1f) {
            fadeInFinished = false;
            const float min = 0.5f;
            const float max = 4f;
            fadeInClock.Restart();
            Core.renderer.charactersModifier = (position, character) => {
                float posRandom = positionRandoms[position] * (max - min) + min;
                float time = fadeInClock.ElapsedTime.AsSeconds();
                if(time * speed * posRandom < 1f) fadeInFinished = false;
                return ((Vector2f)position, new RenderCharacter(Renderer.AnimateColor(time,
                        currentBackground, character.background, speed * posRandom),
                    Renderer.AnimateColor(time,
                        currentBackground, character.foreground, speed * posRandom),
                    character));
            };
        }
        public static void FadeOut(float speed = 1f) {
            fadeOutFinished = false;
            const float min = 0.5f;
            const float max = 4f;
            fadeOutClock.Restart();
            Core.renderer.charactersModifier = (position, character) => {
                float posRandom = positionRandoms[position] * (max - min) + min;
                float time = fadeOutClock.ElapsedTime.AsSeconds();
                if(time * speed * posRandom < 1f) fadeOutFinished = false;
                return ((Vector2f)position, new RenderCharacter(Renderer.AnimateColor(time,
                        character.background, currentBackground, speed * posRandom),
                    Renderer.AnimateColor(time,
                        character.foreground, currentBackground, speed * posRandom),
                    character));
            };
        }

        private static float _menusAnimTime;
        private static void DrawMenusAnim() {
            Color background = currentBackground;
            Color menusAnimMax = ColorScheme.GetColor("menus_anim_max");
            Color transparent = ColorScheme.GetColor("transparent");
            for(int x = -3; x < Core.renderer.width + 3; x++) {
                for(int y = -3; y < Core.renderer.height + 3; y++) {
                    if(x % 3 != 0 || y % 3 != 0) continue;
                    float noiseX = (float)perlin.Get(x / 10f, y / 10f, _menusAnimTime / 2f) - 0.5f;
                    float noiseY = (float)perlin.Get(x / 10f, y / 10f, _menusAnimTime / 2f + 100f) - 0.5f;
                    float noise = MathF.Abs(noiseX * noiseY);
                    float xOffset = (Core.renderer.mousePositionF.X / Core.renderer.width - 0.5f) * noise * -100f;
                    float yOffset = (Core.renderer.mousePositionF.Y / Core.renderer.width - 0.5f) * noise * -100f;
                    Color useColor = Renderer.AnimateColor(noise, background, menusAnimMax, 30f);
                    float xPos = x + noiseX * 10f + xOffset;
                    float yPos = y + noiseY * 10f + yOffset;
                    int flooredX = (int)xPos;
                    int flooredY = (int)yPos;
                    for(int useX = flooredX; useX <= flooredX + 1; useX++) {
                        for(int useY = flooredY; useY <= flooredY + 1; useY++) {
                            float percentX = 1f - MathF.Abs(xPos - useX);
                            float percentY = 1f - MathF.Abs(yPos - useY);
                            float percent = percentX * percentY;
                            Color posColor = Renderer.LerpColors(background, useColor, percent);
                            Core.renderer.SetCellColor(new Vector2i(useX, useY), transparent, posColor);
                        }
                    }
                }
            }
            _menusAnimTime += Core.deltaTime * menusAnimBPM / 120f;
        }
        //static Button _notificationsMenuButton;
        private static void DrawMainMenu() {
            DrawMenusAnim();
            Core.renderer.DrawText(zero, mainMenuText);
            Core.renderer.DrawText(new Vector2i(1, 2), $"PPR v{Core.version}");
            Core.renderer.DrawText(new Vector2i(1, 3), $"PRR v{Core.prrVersion}");
            DrawNowPlaying(true);
            // ReSharper disable once HeapView.ObjectAllocation
            // ReSharper disable once HeapView.ObjectAllocation.Possible
            foreach(Button button in _mainMenuButtons.Where(button => button.Draw()))
                switch(button.id) {
                    case "mainMenu.play":
                    case "mainMenu.edit":
                        Game.editing = button.id == "mainMenu.edit";
                        Core.renderer.window.SetKeyRepeatEnabled(Game.editing);
                        Game.auto = false;
                        Game.menu = Menu.LevelSelect;
                        break;
                    case "mainMenu.settings": Game.menu = Menu.Settings;
                        break;
                    case "mainMenu.exit": Game.Exit();
                        break;
                    case "mainMenu.sfml": Helper.OpenURL("https://sfml-dev.org");
                        break;
                    case "mainMenu.github": Helper.OpenURL("https://github.com/ppr-game/PPR");
                        break;
                    case "mainMenu.discord": Helper.OpenURL("https://discord.gg/AuYUVs5");
                        break;
                }
            /*if(_showNotificationsMenu) DrawNotificationsMenu();
            if(!_notificationsMenuButton.Draw()) return;
            _showNotificationsMenu = !_showNotificationsMenu;
            _notificationsMenuButton.selected = _showNotificationsMenu;
            DrawNotificationsMenu();*/
        }
        //static void DrawNotificationsMenu() => Core.renderer.DrawText(new Vector2i(79, 0), notificationsText, Renderer.Alignment.Right, true);
        public static readonly Vector2i scoresPos = new Vector2i(1, 12);
        private static void DrawLevelSelect() {
            DrawMenusAnim();
            Core.renderer.DrawText(zero, levelSelectText);
            foreach((string levelName, LevelSelectLevel level) in levelSelectLevels) {
                if(level.button.position.Y < 12 || level.button.position.Y > 38) continue;
                if(level.button.Draw()) {
                    string levelPath = Path.Join("levels", levelName);
                    string musicPath = SoundManager.GetSoundFilePath(Path.Join(levelPath, "music"));
                    if(File.Exists(musicPath)) {
                        SoundManager.currentMusicPath = musicPath;
                        SoundManager.music.Stop();
                        SoundManager.music = new Music(musicPath) { Volume = Settings.GetInt("musicVolume") };
                        SoundManager.music.Play();
                    }

                    currSelectedLevel = levelName;
                }

                level.button.selected = levelName == currSelectedLevel;

                if(!level.button.selected) continue;
                foreach((string diffName, LevelSelectDiff diff) in level.diffs) {
                    if(diff.button.position.Y < 40 || diff.button.position.Y > 49) continue;
                    if(diff.button.Draw()) {
                        _lastLevel = levelName;
                        _lastDiff = diffName;
                        string path = Path.Join("levels", _lastLevel);
                        Game.menu = Menu.Game;
                        Game.menuSwitchedCallback += (_, __) => {
                            Map.LoadLevelFromPath(path, _lastLevel, _lastDiff);
                            Game.RecalculatePosition();
                        };
                    }
                        
                    if((diff.button.currentState == Button.State.Hovered ||
                        diff.button.currentState == Button.State.Clicked) &&
                       diff.button.prevFrameState != Button.State.Hovered &&
                       diff.button.prevFrameState != Button.State.Clicked) {
                        string globalScriptPath = Path.Join("levels", levelName, "script.lua");
                        string diffScriptPath = Path.Join("levels", levelName, $"{diffName}.lua");
                        _showLuaPrompt = File.Exists(globalScriptPath) || File.Exists(diffScriptPath);

                        currSelectedDiff = diffName;
                    }
                    
                    diff.button.selected = diffName == currSelectedDiff;

                    if(!diff.button.selected) continue;
                    DrawMetadata(diff.metadata);
                    DrawScores(diff.scores);
                }
            }
            foreach(Button button in _levelSelectButtons)
                switch(button.text) {
                    case "AUTO" when !Game.editing: {
                        if(button.Draw()) Game.auto = !Game.auto;
                        button.selected = Game.auto;
                        break;
                    }
                    case "BACK" when button.Draw(): Game.menu = Menu.Main;
                        break;
                }
            
            DrawNowPlaying();
        }
        private static readonly Vector2i metaLengthPos = new Vector2i(56, 12);
        private static readonly Vector2i metaDiffPos = new Vector2i(56, 13);
        private static readonly Vector2i metaBPMPos = new Vector2i(56, 14);
        private static readonly Vector2i metaAuthorPos = new Vector2i(56, 15);

        private static bool _showLuaPrompt;
        private static readonly Vector2i luaPromptPos = new Vector2i(56, 46);

        private static readonly Vector2i metaObjCountPos = new Vector2i(56, 48);
        private static readonly Vector2i metaSpdCountPos = new Vector2i(56, 49);
        private static void DrawMetadata(LevelMetadata? metadata) {
            if(metadata == null) return;
            Core.renderer.DrawText(metaLengthPos, $"LENGTH:{metadata.Value.length}");
            Core.renderer.DrawText(metaDiffPos, $"DIFFICULTY:{metadata.Value.displayDifficulty}");
            Core.renderer.DrawText(metaBPMPos, $"BPM:{metadata.Value.bpm}");
            Core.renderer.DrawText(metaAuthorPos, $"AUTHOR:{metadata.Value.author}");
            
            if(_showLuaPrompt)
                Core.renderer.DrawText(luaPromptPos, "○ Contains Lua", ColorScheme.GetColor("lua_prompt"));

            Core.renderer.DrawText(metaObjCountPos, $"objects:{metadata.Value.objectCount.ToString()}");
            Core.renderer.DrawText(metaSpdCountPos, $"speeds:{metadata.Value.speedsCount.ToString()}");
        }
        private static void DrawScores(IReadOnlyCollection<LevelScore> scores) {
            if(scores == null) return;
            // ReSharper disable once HeapView.ObjectAllocation.Possible
            foreach(LevelScore score in scores) {
                if(score.scorePosition.Y >= 12 && score.scorePosition.Y <= 49)
                    Core.renderer.DrawText(score.scorePosition, score.scoreStr, ColorScheme.GetColor("score"));
                if(score.accComboPosition.Y >= 12 && score.accComboPosition.Y <= 49) {
                    Core.renderer.DrawText(score.accComboPosition, score.accuracyStr, score.accuracyColor);
                    Core.renderer.DrawText(score.accComboDividerPosition, "│", ColorScheme.GetColor("combo"));
                    Core.renderer.DrawText(score.maxComboPosition, score.maxComboStr, score.maxComboColor);
                }
                if(score.scoresPosition.Y >= 12 && score.scoresPosition.Y <= 49)
                    DrawMiniScores(score.scoresPosition, score.scores);
                if(score.linePosition.Y >= 12 && score.linePosition.Y <= 49) Core.renderer.DrawText(score.linePosition,
                        score.linePosition.Y == 39 ? "├───────────────────────┼" : "├───────────────────────┤");
            }
        }

        private static readonly Vector2i levelNamePos = new Vector2i(0, 0);
        private static readonly Vector2i musicTimePos = new Vector2i(79, 0);
        private static readonly Vector2i scorePos = new Vector2i(0, 57);
        private static readonly Vector2i accPos = new Vector2i(0, 58);
        private static readonly Vector2i comboPos = new Vector2i(0, 59);
        private static readonly Vector2i miniScoresPos = new Vector2i(25, 59);
        private static readonly Vector2i bpmPos = new Vector2i(0, 57);
        private static readonly Vector2i timePos = new Vector2i(0, 58);
        private static readonly Vector2i offsetPos = new Vector2i(0, 59);
        private static readonly Vector2i hpDrainPos = new Vector2i(20, 57);
        private static readonly Vector2i hpRestoragePos = new Vector2i(20, 58);
        private static readonly Vector2i musicOffsetPos = new Vector2i(20, 59);
        private static Slider _musicSpeedSlider;
        private static void DrawGame() {
            if(Game.editing) {
                foreach(Button button in _levelEditorButtons) {
                    if(button.id == "editor.playPause") button.text = Game.playing ? "║" : "►";
                    if(button.Draw()) {
                        switch(button.text) {
                            case "►":
                                Game.UpdateMusicTime();
                                Game.playing = true;
                                break;
                            case "║":
                                Game.playing = false;
                                Game.RoundSteps();
                                Game.UpdateTime();
                                break;
                        }

                        bool boost = Keyboard.IsKeyPressed(Keyboard.Key.LShift) ||
                                     Keyboard.IsKeyPressed(Keyboard.Key.RShift);
                        switch(button.id) {
                            case "editor.hp.drain.up": Map.currentLevel.metadata.hpDrain++;
                                break;
                            case "editor.hp.drain.down": Map.currentLevel.metadata.hpDrain--;
                                break;
                            case "editor.hp.restorage.up": Map.currentLevel.metadata.hpRestorage++;
                                break;
                            case "editor.hp.restorage.down": Map.currentLevel.metadata.hpRestorage--;
                                break;
                            case "editor.music.offset.up": Map.currentLevel.metadata.musicOffset += boost ? 10 : 1;
                                break;
                            case "editor.music.offset.down": Map.currentLevel.metadata.musicOffset += boost ? 10 : 1;
                                break;
                        }
                    }
                }
                Core.renderer.DrawText(bpmPos, $"BPM: {Game.currentBPM.ToString()}", ColorScheme.GetColor("bpm"));
                TimeSpan curTime = TimeSpan.FromMilliseconds(Game.levelTime.AsMilliseconds());
                Core.renderer.DrawText(timePos,
                    $"TIME: {(curTime < TimeSpan.Zero ? "'-'" : "")}{curTime.ToString($"{(curTime.Hours != 0 ? "h':'mm" : "m")}':'ss'.'fff")}",
                    ColorScheme.GetColor("time"));
                Core.renderer.DrawText(offsetPos, $"OFFSET: {Game.roundedOffset.ToString()} ({Game.roundedSteps.ToString()})",
                    ColorScheme.GetColor("offset"));

                Core.renderer.DrawText(hpDrainPos, $"    HP DRAIN: {Map.currentLevel.metadata.hpDrain.ToString()}", ColorScheme.GetColor("hp_drain"));
                Core.renderer.DrawText(hpRestoragePos, $"    HP RESTORAGE: {Map.currentLevel.metadata.hpRestorage.ToString()}", ColorScheme.GetColor("hp_restorage"));

                Core.renderer.DrawText(musicOffsetPos,
                    $"    MUSIC OFFSET: {Map.currentLevel.metadata.musicOffset.ToString()} MS", ColorScheme.GetColor("music_offset"));

                if(_musicSpeedSlider.Draw()) SoundManager.music.Pitch = _musicSpeedSlider.value / 100f;

                DrawProgress();
                DrawLevelName(levelNamePos, ColorScheme.GetColor("game_level_name"));
                DrawEditorDifficulty(musicTimePos, ColorScheme.GetColor("game_music_time"));
            }
            else {
                DrawHealth();
                DrawProgress();
                DrawScore(scorePos, ColorScheme.GetColor("score"));
                DrawAccuracy(accPos);
                DrawCombo(comboPos);
                DrawMiniScores(miniScoresPos, ScoreManager.scores);
                DrawLevelName(levelNamePos, ColorScheme.GetColor("game_level_name"));
                DrawMusicTime(musicTimePos, ColorScheme.GetColor("game_music_time"));
                LevelMetadata metadata = Map.currentLevel.metadata;

                if(!metadata.skippable ||
                   Game.levelTime.AsMilliseconds() >= Map.currentLevel.metadata.skipTime ||
                   !_skipButton.Draw()) return;
                
                Game.levelTime = Time.FromMilliseconds(Map.currentLevel.metadata.skipTime);
                Game.UpdateMusicTime();
                Game.UpdatePresence();
            }
        }
        private static void DrawHealth() {
            for(int x = 0; x < 80; x++) {
                float rate = 3.5f + healthAnimRateOffsets[x];
                Core.renderer.SetCellColor(new Vector2i(x, 1), ColorScheme.GetColor("transparent"),
                    Renderer.AnimateColor(healthAnimTimes[x], prevHealthColors[x], healthColors[x], rate));
                healthAnimTimes[x] += Core.deltaTime;
            }
        }
        private static void DrawProgress() {
            for(int x = 0; x < 80; x++) {
                float rate = 3.5f + progressAnimRateOffsets[x];
                Core.renderer.SetCellColor(new Vector2i(x, 0), ColorScheme.GetColor("transparent"),
                    Renderer.AnimateColor(progressAnimTimes[x], prevProgressColors[x], progressColors[x], rate));
                progressAnimTimes[x] += Core.deltaTime;
            }
        }
        private static int _scoreChange;
        public static int prevScore;
        private static float _newScoreAnimationTime;
        private static float _scoreAnimationRate = 2f;
        private static void DrawScore(Vector2i position, Color color) {
            string scoreStr = $"SCORE: {ScoreManager.score.ToString()}";
            Core.renderer.DrawText(position, scoreStr, color);
            if(prevScore != ScoreManager.score) {
                if(_newScoreAnimationTime >= 1f / _scoreAnimationRate) _scoreChange = 0;
                _newScoreAnimationTime = 0f;
                _scoreChange += ScoreManager.score - prevScore;
            }
            Core.renderer.DrawText(new Vector2i(position.X + scoreStr.Length + 2, position.Y),
                $"+{_scoreChange.ToString()}",
                Renderer.AnimateColor(_newScoreAnimationTime, color, ColorScheme.GetColor("transparent"),
                    _scoreAnimationRate));
            _newScoreAnimationTime += Core.deltaTime;

            prevScore = ScoreManager.score;
        }
        private static void DrawAccuracy(Vector2i position) => Core.renderer.DrawText(position, $"ACCURACY: {ScoreManager.accuracy.ToString()}%",
            ScoreManager.GetAccuracyColor(ScoreManager.accuracy));
        private static void DrawCombo(Vector2i position, bool maxCombo = false) {
            string prefix = ScoreManager.accuracy >= 100 ? "PERFECT " : ScoreManager.scores[0] <= 0 ? "FULL " : maxCombo ? "MAX " : "";
            Color color = ScoreManager.GetComboColor(ScoreManager.accuracy, ScoreManager.scores[0]);
            Core.renderer.DrawText(position, $"{prefix}COMBO: {(maxCombo ? ScoreManager.maxCombo : ScoreManager.combo).ToString()}",
                color, ColorScheme.GetColor("transparent"));
        }
        private static void DrawMiniScores(Vector2i position, int[] scores) {
            string scores0Str = scores[0].ToString();
            Core.renderer.DrawText(position, scores0Str, currentBackground,
                ColorScheme.GetColor("miss"));

            string scores1Str = scores[1].ToString();
            int x1 = position.X + scores0Str.Length + 1;
            Core.renderer.DrawText(new Vector2i(x1, position.Y), scores1Str, currentBackground,
                ColorScheme.GetColor("hit"));

            Core.renderer.DrawText(new Vector2i(x1 + scores1Str.Length + 1, position.Y), scores[2].ToString(), 
                currentBackground,
                ColorScheme.GetColor("perfect_hit"));
        }
        private static void DrawScores(Vector2i position) {
            int posXOffseted = position.X + 15;
            Core.renderer.DrawText(position, "MISSES:", ColorScheme.GetColor("miss"));
            Core.renderer.DrawText(new Vector2i(posXOffseted, position.Y), ScoreManager.scores[0].ToString(),
                currentBackground,
                ColorScheme.GetColor("miss"));

            int posYHits = position.Y + 2;
            Core.renderer.DrawText(new Vector2i(position.X, posYHits), "HITS:", ColorScheme.GetColor("hit"));
            Core.renderer.DrawText(new Vector2i(posXOffseted, posYHits), ScoreManager.scores[1].ToString(),
                currentBackground,
                ColorScheme.GetColor("hit"));

            int posYPerfectHits = position.Y + 4;
            Core.renderer.DrawText(new Vector2i(position.X, posYPerfectHits), "PERFECT HITS:", ColorScheme.GetColor("perfect_hit"));
            Core.renderer.DrawText(new Vector2i(posXOffseted, posYPerfectHits), ScoreManager.scores[2].ToString(),
                currentBackground,
                ColorScheme.GetColor("perfect_hit"));
        }
        private static void DrawLevelName(Vector2i position, Color color, bool invertOnDarkBG = true) =>
            Core.renderer.DrawText(position,
            $"{Map.currentLevel.metadata.name} [{Map.currentLevel.metadata.displayDiff}] : {Map.currentLevel.metadata.author}",
            color, Renderer.Alignment.Left, false, invertOnDarkBG);
        private static void DrawMusicTime(Vector2i position, Color color) {
            TimeSpan timeSpan = TimeSpan.FromMilliseconds(Game.levelTime.AsMilliseconds());
            Core.renderer.DrawText(position,
                $"{Calc.TimeSpanToLength(timeSpan)}/{Map.currentLevel.metadata.totalLength}", color,
                Renderer.Alignment.Right, false, true);
        }
        private static void DrawEditorDifficulty(Vector2i position, Color color) => Core.renderer.DrawText(position,
            $"DIFFICULTY: {Map.currentLevel.metadata.displayDifficulty}", color,
            Renderer.Alignment.Right, false, true);
        private static readonly Vector2i passFailText = new Vector2i(40, 5);
        private static readonly Vector2i lastLevelPos = new Vector2i(2, 13);
        private static readonly Vector2i lastScorePos = new Vector2i(2, 16);
        private static readonly Vector2i lastAccPos = new Vector2i(2, 18);
        private static readonly Vector2i lastScoresPos = new Vector2i(25, 16);
        private static readonly Vector2i lastMaxComboPos = new Vector2i(2, 20);
        private static void DrawLastStats() {
            DrawMenusAnim();
            Core.renderer.DrawText(zero, lastStatsText);
            string text = "PAUSE";
            Color color = ColorScheme.GetColor("pause");
            if(!Game.editing && Game.statsState != StatsState.Pause) {
                if(Game.statsState == StatsState.Pass) {
                    text = "PASS";
                    color = ColorScheme.GetColor("pass");
                }
                else {
                    text = "FAIL";
                    color = ColorScheme.GetColor("fail");
                }
            }
            Core.renderer.DrawText(passFailText, text, color, Renderer.Alignment.Center);
            DrawLevelName(lastLevelPos, ColorScheme.GetColor("stats_level_name"), false);
            if(!Game.editing) {
                DrawScore(lastScorePos, ColorScheme.GetColor("score"));
                DrawAccuracy(lastAccPos);
                DrawScores(lastScoresPos);
                DrawCombo(lastMaxComboPos, true);
            }
            DrawSettingsList(true);
            if(Game.editing)
                foreach(Button button in _editorLastStatsButtons)
                    switch(button.id) {
                        case "lastStats.continue": LastStatsContinue(button);
                            break;
                        case "lastStats.save":
                        case "lastStats.saveAndExit":
                            if(button.Draw()) {
                                Game.changed = false;
                                string path = Path.Join("levels", _lastLevel);
                                _ = Directory.CreateDirectory(path);
                                File.WriteAllText(Path.Join(path, $"{_lastDiff}.txt"), Map.TextFromLevel(Map.currentLevel));
                                if(button.id == "lastStats.saveAndExit") LastStatsExit();
                            }
                            if(button.text.EndsWith('*') && !Game.changed) {
                                button.text = button.text.Remove(button.text.Length - 1);
                                button.width--;
                            }
                            else if(!button.text.EndsWith('*') && Game.changed) {
                                button.text = $"{button.text}*";
                                button.width++;
                            }
                            break;
                        case "lastStats.exit":
                            if(button.Draw()) {
                                Game.changed = false;
                                LastStatsExit();
                            }
                            break;
                    }
            else
                foreach(Button button in _gameLastStatsButtons)
                    switch(button.id) {
                        case "lastStats.continue": LastStatsContinue(button);
                            break;
                        case "lastStats.restart":
                            if(!Game.editing && button.Draw()) {
                                Game.menu = Menu.Game;
                                Game.menuSwitchedCallback += (_, __) =>
                                    Map.LoadLevelFromPath(Path.Join("levels", _lastLevel), _lastLevel, _lastDiff);
                            }
                            break;
                        case "lastStats.auto":
                            if(!Game.editing && button.Draw()) Game.auto = !Game.auto;
                            button.selected = Game.auto;
                            break;
                        case "lastStats.exit": if(button.Draw()) LastStatsExit();
                            break;
                    }
        }
        private static void LastStatsContinue(Button button) {
            if(Map.currentLevel.objects.Count > 0 && Game.health > 0 && button.Draw()) Game.menu = Menu.Game;
        }
        private static void LastStatsExit() {
            Game.EndGame();
            _musicSpeedSlider.value = 100;
        }

        private static readonly Vector2i audioGroupTextPos = new Vector2i(2, 13);
        private static readonly Vector2i audioSwitchPos = new Vector2i(4, 19);
        private static readonly List<Button> audioSwitchButtonsList = new List<Button>();

        private static readonly Vector2i graphicsGroupTextPos = new Vector2i(2, 22);
        private static readonly Vector2i fontSwitchPos = new Vector2i(4, 32);
        private static readonly List<Button> fontSwitchButtonsList = new List<Button>();
        private static readonly Vector2i colorSchemeSwitchPos = new Vector2i(4, 34);
        private static readonly List<Button> colorSchemeSwitchButtonsList = new List<Button>();

        private static readonly Vector2i advancedGroupTextPos = new Vector2i(2, 37);

        private static string IncreaseFolderSwitchDirectory(string currentPath, string basePath, int at) {
            // Disassemble the path
            List<string> fullDirNames = currentPath.Split(Path.DirectorySeparatorChar).ToList();
            while(fullDirNames.Count > at + 1) fullDirNames.RemoveAt(fullDirNames.Count - 1);
            string fullDir = Path.Join(fullDirNames.ToArray());
            string inDir = Path.GetDirectoryName(fullDir);
            string[] inDirNames = Directory.GetDirectories(Path.Join(basePath, inDir ?? ""))
                .Select(Path.GetFileName).ToArray();

            // Move to the next folder
            int curPathIndex = Array.IndexOf(inDirNames, fullDirNames.Last());
            int nextIndex = curPathIndex + 1;
            fullDirNames.RemoveAt(at);
            fullDirNames.Add(inDirNames[nextIndex >= inDirNames.Length ? 0 : nextIndex]);

            // Assemble the path back
            string newPath = Path.Join(fullDirNames.ToArray());
            string[] newPathDirs = Directory.GetDirectories(Path.Join(basePath, newPath));
            while(newPathDirs.Length > 0) {
                newPath = Path.Join(newPath, Path.GetFileName(newPathDirs[0]) ?? string.Empty);
                newPathDirs = Directory.GetDirectories(Path.Join(basePath, newPath));
            }
            return newPath;
        }
        private static void UpdateAllFolderSwitchButtons() {
            UpdateFolderSwitchButtons(audioSwitchButtonsList, Settings.GetPath("audio"), audioSwitchPos.X,
                audioSwitchPos.Y, 7);
            UpdateFolderSwitchButtons(fontSwitchButtonsList, Settings.GetPath("font"), fontSwitchPos.X,
                fontSwitchPos.Y, 5);
            UpdateFolderSwitchButtons(colorSchemeSwitchButtonsList, Settings.GetPath("colorScheme"),
                colorSchemeSwitchPos.X, colorSchemeSwitchPos.Y, 13);
        }
        private static void UpdateFolderSwitchButtons(IList<Button> buttonsList, string path, int baseX, int baseY, int xOffset) {
            buttonsList.Clear();
            UpdateFolderSwitchButton(buttonsList, path, baseX, baseY, xOffset);
        }
        private static void UpdateFolderSwitchButton(IList<Button> buttonsList, string path, int baseX, int baseY, int xOffset) {
            while(true) {
                if(path == null) return;
                string[] names = path.Split(Path.DirectorySeparatorChar);

                string prevDir = Path.GetDirectoryName(path) ?? string.Empty;
                Vector2i position = new Vector2i(baseX + xOffset + (names.Length > 1 ? 1 : 0) + prevDir.Length, baseY);
                string text = names[^1];
                buttonsList.Insert(0, new Button(position, text, "settings.folderButton", text.Length));

                string nextPath = Path.GetDirectoryName(path);
                if(nextPath != "") {
                    path = nextPath;
                    continue;
                }

                break;
            }
        }
        private static void DrawSettingsList(bool pauseMenu = false) {
            if(pauseMenu) {
                musicVolumeSlider.position = new Vector2i(78, 55);
                musicVolumeSlider.align = Renderer.Alignment.Right;
                musicVolumeSlider.swapTexts = true;

                soundsVolumeSlider.position = new Vector2i(78, 57);
                soundsVolumeSlider.align = Renderer.Alignment.Right;
                soundsVolumeSlider.swapTexts = true;
            }
            else {
                Core.renderer.DrawText(audioGroupTextPos, "[ AUDIO ]", ColorScheme.GetColor("settings_header_audio"));
                musicVolumeSlider.position = new Vector2i(4, 15);
                musicVolumeSlider.align = Renderer.Alignment.Left;
                musicVolumeSlider.swapTexts = false;

                soundsVolumeSlider.position = new Vector2i(4, 17);
                soundsVolumeSlider.align = Renderer.Alignment.Left;
                soundsVolumeSlider.swapTexts = false;

                Core.renderer.DrawText(audioSwitchPos, "SOUNDS", ColorScheme.GetColor("settings"));
                for(int i = audioSwitchButtonsList.Count - 1; i >= 0; i--)
                    if(audioSwitchButtonsList[i].Draw()) {
                        Settings.SetPath("audio",
                            IncreaseFolderSwitchDirectory(Settings.GetPath("audio"),
                                Path.Join("resources", "audio"), i));
                        UpdateFolderSwitchButtons(audioSwitchButtonsList, Settings.GetPath("audio"), audioSwitchPos.X,
                            audioSwitchPos.Y, 7);
                    }

                Core.renderer.DrawText(graphicsGroupTextPos, "[ GRAPHICS ]", ColorScheme.GetColor("settings_header_graphics"));
                if(bloomSwitch.Draw()) Settings.SetBool("bloom", bloomSwitch.selected = !bloomSwitch.selected);
                if(fullscreenSwitch.Draw())
                    Settings.SetBool("fullscreen", fullscreenSwitch.selected = !fullscreenSwitch.selected);
                fpsLimitSlider.rightText = fpsLimitSlider.value < 60 ? "V-Sync" :
                    fpsLimitSlider.value > 960 ? "Unlimited" : "[value]";
                if(fpsLimitSlider.Draw()) Settings.SetInt("fpsLimit", fpsLimitSlider.value);
                if(uppercaseSwitch.Draw())
                    Settings.SetBool("uppercaseNotes", uppercaseSwitch.selected = !uppercaseSwitch.selected);
                Core.renderer.DrawText(fontSwitchPos, "FONT", ColorScheme.GetColor("settings"));
                for(int i = fontSwitchButtonsList.Count - 1; i >= 0; i--)
                    if(fontSwitchButtonsList[i].Draw()) {
                        Settings.SetPath("font",
                            IncreaseFolderSwitchDirectory(Settings.GetPath("font"),
                                Path.Join("resources", "fonts"), i));
                        UpdateFolderSwitchButtons(fontSwitchButtonsList, Settings.GetPath("font"), fontSwitchPos.X,
                            fontSwitchPos.Y, 5);
                    }

                Core.renderer.DrawText(colorSchemeSwitchPos, "COLOR SCHEME", ColorScheme.GetColor("settings"));
                for(int i = colorSchemeSwitchButtonsList.Count - 1; i >= 0; i--)
                    if(colorSchemeSwitchButtonsList[i].Draw()) {
                        Settings.SetPath("colorScheme",
                            IncreaseFolderSwitchDirectory(Settings.GetPath("colorScheme"),
                                Path.Join("resources", "colors"), i));
                        UpdateFolderSwitchButtons(colorSchemeSwitchButtonsList, Settings.GetPath("colorScheme"),
                            colorSchemeSwitchPos.X, colorSchemeSwitchPos.Y, 13);
                    }

                Core.renderer.DrawText(advancedGroupTextPos, "[ ADVANCED ]", ColorScheme.GetColor("settings_header_advanced"));
                if(showFpsSwitch.Draw()) Settings.SetBool("showFps", showFpsSwitch.selected = !showFpsSwitch.selected);
            }

            if(musicVolumeSlider.Draw()) Settings.SetInt("musicVolume", musicVolumeSlider.value);
            if(soundsVolumeSlider.Draw()) Settings.SetInt("soundsVolume", soundsVolumeSlider.value);
        }
        //static Button _keybindsButton;
        private static void DrawSettings() {
            DrawMenusAnim();
            Core.renderer.DrawText(zero, settingsText);
            DrawSettingsList();
            //if(_keybindsButton.Draw()) Game.menu = Menu.KeybindsEditor;
        }
        private static void DrawKeybindsEditor() {
            DrawMenusAnim();
            Core.renderer.DrawText(zero, keybindsEditorText);

            int y = 17;
            foreach((string origName, InputKey key) in Bindings.keys) {
                string name = origName.AddSpaces().ToUpper();
                string[] primAndSec = key.asString.Split(',');
                string primary = primAndSec[0];
                string secondary = primAndSec.Length > 1 ? primAndSec[1] : "<NONE>";
                Core.renderer.DrawText(new Vector2i(2, y), name, ColorScheme.GetColor("settings_keybind_name"));
                Core.renderer.DrawText(new Vector2i(37, y), primary, ColorScheme.GetColor("settings_keybind_primary"));
                Core.renderer.DrawText(new Vector2i(59, y), secondary, ColorScheme.GetColor("settings_keybind_secondary"));
                y += 2;
            }
        }
        public static void Draw() {
            switch(Game.menu) {
                case Menu.Main:
                    DrawMainMenu();
                    break;
                case Menu.LevelSelect:
                    DrawLevelSelect();
                    break;
                case Menu.Settings:
                    DrawSettings();
                    break;
                case Menu.KeybindsEditor:
                    DrawKeybindsEditor();
                    break;
                case Menu.Game:
                    DrawGame();
                    break;
                case Menu.LastStats:
                    DrawLastStats();
                    break;
            }

            Lua.DrawUI();
            
            if(Settings.GetBool("showFps"))
                Core.renderer.DrawText(fpsPos, $"{fps.ToString()}/{avgFPS.ToString()} FPS , {tps.ToString()} TPS",
                    fps >= 60 ? ColorScheme.GetColor("fps_good") : fps > 20 ? ColorScheme.GetColor("fps_ok") : 
                        ColorScheme.GetColor("fps_bad"), Renderer.Alignment.Right);

            prevMousePosition = Core.renderer.mousePosition;
        }
        private static readonly Vector2i fpsPos = new Vector2i(79, 59);
    }
}
