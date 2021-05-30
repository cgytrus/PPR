﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

using MoonSharp.Interpreter;

using NCalc;

using Newtonsoft.Json;

using PPR.Main;
using PPR.Main.Levels;
using PPR.Properties;
using PPR.UI.Animations;
using PPR.UI.Elements;

using PRR;

using SFML.Graphics;
using SFML.System;

using Alignment = PRR.Renderer.Alignment;

namespace PPR.UI {
    public static class Manager {
        public static int fps = 0;
        public static int avgFPS = 0;
        public static int tempAvgFPS = 0;
        public static int tempAvgFPSCounter = 0;
        public static int tps = 0;

        public static Layout currentLayout { get; private set; }
        public static Dictionary<string, AnimationSettings> animationPresets { get; private set; }
        public static
            Dictionary<string, Func<float, Dictionary<string, double>, ReadOnlyDictionary<string, double>,
                Func<Vector2i, RenderCharacter, (Vector2i, RenderCharacter)>>> animations { get; private set; }

        private static readonly Random random = new Random();
        //private static readonly Perlin perlin = new Perlin();

        public static string currSelectedLevel;
        public static string currSelectedDiff;
        public static Dictionary<string, LevelSelectLevel> levelSelectLevels;
        
        /*public static Slider musicVolumeSlider;
        public static Slider soundsVolumeSlider;
        
        public static Button bloomSwitch;
        public static Button fullscreenSwitch;
        public static Slider fpsLimitSlider;
        public static Button uppercaseSwitch;
        
        public static Button showFpsSwitch;*/

        // ReSharper disable once NotAccessedField.Global
        public static int menusAnimBPM = 120;
        public static IReadOnlyDictionary<Vector2i, float> positionRandoms => _positionRandoms;

        public static Vector2i prevMousePosition { get; private set; }

        /*private static readonly string[] settingsText = File.ReadAllLines(Path.Join("resources", "ui", "settings.txt"));
        private static readonly string[] keybindsEditorText = File.ReadAllLines(Path.Join("resources", "ui", "keybinds.txt"));
        private static readonly string[] levelSelectText = File.ReadAllLines(Path.Join("resources", "ui", "levelSelect.txt"));
        private static readonly string[] lastStatsText = File.ReadAllLines(Path.Join("resources", "ui", "lastStats.txt"));*/
        //static readonly string[] notificationsText = File.ReadAllLines(Path.Join("resources", "ui", "notifications.txt"));

        /*private static List<Button> _levelEditorButtons;

        private static Button _skipButton;*/

        //static bool _showNotificationsMenu;

        // don't mind this monstrosity over here
        public static void LoadLayout(string path) {
            string animationsPath = Path.Join(path, "animations.json");
            string animationPresetsPath = Path.Join(path, "animationPresets.json");
            string layoutPath = Path.Join(path, "layout.json");
            string scriptPath = Path.Join(path, "script.lua");

            if(!File.Exists(animationsPath))
                throw new FileNotFoundException("Could not load the layout because the animations file was not found.",
                    animationsPath);
            if(!File.Exists(animationPresetsPath))
                throw new FileNotFoundException(
                    "Could not load the layout because the animation presets file was not found.",
                    animationPresetsPath);
            if(!File.Exists(layoutPath))
                throw new FileNotFoundException("Could not load the layout because the layout file was not found.",
                    layoutPath);
            if(!File.Exists(scriptPath))
                throw new FileNotFoundException("Could not load the layout because the script file was not found.",
                    scriptPath);

            LoadAnimations(animationsPath, animationPresetsPath);

            Lua.Manager.UnsubscribeAllEvents(currentLayout?.script);
            Lua.Manager.consoles.Remove(currentLayout?.script);
            
            Script script = new Script(CoreModules.Preset_SoftSandbox);
            Lua.Manager.InitializeConsole(script);
            
            currentLayout = new Layout(script);
            
            Dictionary<string, DeserializedElement> layout =
                JsonConvert.DeserializeObject<Dictionary<string, DeserializedElement>>(File.ReadAllText(layoutPath));
            foreach((string id, DeserializedElement elem) in layout) {
                string type = elem.type ?? "panel";

                bool enabled = elem.enabled;
                List<string> tags = elem.tags;
                Dictionary<string, int> posDict = elem.position;
                Dictionary<string, int> sizeDict = elem.size;
                Dictionary<string, float> anchorDict = elem.anchor;
                Vector2i position = posDict == null ? new Vector2i() :
                    new Vector2i(posDict.GetValueOrDefault("x", 0), posDict.GetValueOrDefault("y", 0));
                Vector2i size = sizeDict == null ? new Vector2i(1, 1) :
                    new Vector2i(sizeDict.GetValueOrDefault("x", 1), sizeDict.GetValueOrDefault("y", 1));
                Vector2f anchor = anchorDict == null ? new Vector2f() :
                    new Vector2f(anchorDict.GetValueOrDefault("x", 0f), anchorDict.GetValueOrDefault("y", 0f));
                string parentId = elem.parent ?? string.Join('.', id.Split('.').SkipLast(1).ToArray());
                Element parent = currentLayout.elements.GetValueOrDefault(parentId, null);
                string text = elem.path == null ? elem.text : File.ReadAllText(Path.Join(path, elem.path));
                Alignment align = (elem.align ?? "left") switch {
                    "right" => Alignment.Right,
                    "center" => Alignment.Center,
                    _ => Alignment.Left
                };
                bool replacingSpaces = elem.replacingSpaces;
                bool invertOnDarkBackground = elem.invertOnDarkBackground;
                int width = elem.width;
                int minValue = elem.minValue;
                int maxValue = elem.maxValue;
                int defaultValue = elem.defaultValue;
                string leftText = elem.leftText ?? "";
                string rightText = elem.rightText ?? "";
                bool swapTexts = elem.swapTexts;
                AnimationSettings inAnimation = animationPresets.GetValueOrDefault(elem.inAnimationPreset ?? type);
                AnimationSettings outAnimation = animationPresets.GetValueOrDefault(elem.outAnimationPreset ?? type);

                Element element = type switch {
                    "panel" => new Panel(id, tags, position, size, anchor, parent),
                    "filledPanel" => new FilledPanel(id, tags, position, size, anchor, parent),
                    "mask" => new Mask(id, tags, position, size, anchor, parent),
                    "text" => new Elements.Text(id, tags, position, anchor, parent, text, align, replacingSpaces,
                        invertOnDarkBackground),
                    "button" => new Button(id, tags, position, width, anchor, parent, text, null, align),
                    "slider" => new Slider(id, tags, position, width, anchor, parent, minValue, maxValue, defaultValue,
                        leftText, rightText, align, swapTexts),
                    "progressBar" => new ProgressBar(id, defaultValue, maxValue, inAnimation, outAnimation, tags,
                        position, size, anchor, parent),
                    _ => null
                };
                element.enabled = enabled;

                if(element != null) currentLayout.AddElement(id, element);
            }
            
            script.DoFile(scriptPath);
        }

        private static void LoadAnimations(string animationsPath, string presetsPath) {
            AnimationContext.customVars.Clear();

            Dictionary<string, DeserializedAnimation> deserializedAnimations =
                JsonConvert.DeserializeObject<Dictionary<string, DeserializedAnimation>>(File.ReadAllText(animationsPath));

            Dictionary<string, CustomAnimation> customAnimations =
                new Dictionary<string, CustomAnimation>(deserializedAnimations.Count);
            foreach((string name, DeserializedAnimation animation) in deserializedAnimations)
                customAnimations.Add(name, new CustomAnimation(animation));

            animations =
                new Dictionary<string, Func<float, Dictionary<string, double>, ReadOnlyDictionary<string, double>,
                    Func<Vector2i, RenderCharacter, (Vector2i, RenderCharacter)>>>();
            foreach((string name, CustomAnimation animation) in customAnimations) {
                animations.Add(name, (time, args, consts) => {
                    AnimationContext context = new AnimationContext();
                    return (pos, character) => {
                        context.x = pos.X;
                        context.y = pos.Y;
                        context.character = character.character;
                        context.bgR = character.background.R;
                        context.bgG = character.background.G;
                        context.bgB = character.background.B;
                        context.bgA = character.background.A;
                        context.fgR = character.foreground.R;
                        context.fgG = character.foreground.G;
                        context.fgB = character.foreground.B;
                        context.fgA = character.foreground.A;
                        context.time = time;
                        context.args = args;
                        context.consts = consts;
                        Vector2i modPos = pos;
                        RenderCharacter modChar = character;
                        modPos = new Vector2i(animation.x?.Invoke(context) ?? modPos.X,
                            animation.y?.Invoke(context) ?? modPos.Y);
                        modChar = new RenderCharacter(animation.character?.Invoke(context)[0] ?? modChar.character,
                            new Color(animation.bgR?.Invoke(context) ?? modChar.background.R,
                                animation.bgG?.Invoke(context) ?? modChar.background.G,
                                animation.bgB?.Invoke(context) ?? modChar.background.B,
                                animation.bgA?.Invoke(context) ?? modChar.background.A),
                            new Color(animation.fgR?.Invoke(context) ?? modChar.foreground.R,
                                animation.fgG?.Invoke(context) ?? modChar.foreground.G,
                                animation.fgB?.Invoke(context) ?? modChar.foreground.B,
                                animation.fgA?.Invoke(context) ?? modChar.foreground.A));
                        return (modPos, modChar);
                    };
                });
            }
            
            animationPresets =
                JsonConvert.DeserializeObject<Dictionary<string, AnimationSettings>>(File.ReadAllText(presetsPath));
        }

        public static Animation AnimateElement(Element element, string animationPreset, Closure endCallback,
            Dictionary<string, double> args) {
            Animation anim = new Animation(animationPresets[animationPreset], endCallback, args);
            if(element == null) {
                foreach(Element elem in new Dictionary<string, Element>(currentLayout.elements).Values)
                    if(elem.parent == null)
                        elem.AddAnimation(anim);
            }
            else element.AddAnimation(anim);
            return anim;
        }

        public static bool StopElementAnimations(Element element, string animation) {
            bool wasPlaying = false;
            foreach(Animation anim in element.animations) {
                if(animation == null || anim.settings.id != animation) continue;
                wasPlaying |= element.StopAnimation(anim);
            }
            return wasPlaying;
        }

        public static bool StopElementAnimation(Element element, Animation animation) => element.StopAnimation(animation);

        /*public static void RecreateButtons() {
            const Alignment center = Alignment.Center;
            const Alignment right = Alignment.Right;
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
        }*/
        
        private static Dictionary<Vector2i, float> _positionRandoms;
        public static void RegenPositionRandoms() {
            _positionRandoms = new Dictionary<Vector2i, float>(Core.renderer.width * Core.renderer.height);
            for(int x = 0; x < Core.renderer.width; x++)
                for(int y = 0; y < Core.renderer.height; y++)
                    _positionRandoms[new Vector2i(x, y)] = random.NextFloat(0f, 1f);
        }
        
        public static void UpdateAnims() {
            bool useScriptCharMod =
                Lua.API.Scripts.Rendering.Renderer.scriptCharactersModifier != null && currentLayout.IsElementEnabled("game");
            if(useScriptCharMod) Core.renderer.charactersModifier = Lua.API.Scripts.Rendering.Renderer.scriptCharactersModifier;

            if(currentLayout.IsElementEnabled("game")) {
                Color? color =
                    Lua.API.Scripts.Rendering.Renderer.scriptBackgroundModifier?.Invoke(ColorScheme.GetColor("background"));
                if(color.HasValue) Core.renderer.background = color.Value;
                else Core.renderer.ResetBackground();
            }
            else Core.renderer.ResetBackground();

            if(useScriptCharMod) return;
            Core.renderer.charactersModifier = null;
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
        private static int Orientation(Vector2i p, Vector2i q, Vector2i r) =>
            Math.Sign((q.Y - p.Y) * (r.X - q.X) - (q.X - p.X) * (r.Y - q.Y));

        /*private static float _menusAnimTime;
        private static void DrawMenusAnim() {
            Color background = Core.renderer.background;
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
        }*/
        
        //static void DrawNotificationsMenu() => Core.renderer.DrawText(new Vector2i(79, 0), notificationsText, Renderer.Alignment.Right, true);

        /*private static readonly Vector2i miniScoresPos = new Vector2i(25, 59);
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
                    if(button.Draw()) {
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

                DrawEditorDifficulty(musicTimePos, ColorScheme.GetColor("game_music_time"));
            }
            else {
                DrawMusicTime(musicTimePos, ColorScheme.GetColor("game_music_time"));
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
        private static void DrawMusicTime(Vector2i position, Color color) {
            TimeSpan timeSpan = TimeSpan.FromMilliseconds(Game.levelTime.AsMilliseconds());
            Core.renderer.DrawText(position,
                $"{Calc.TimeSpanToLength(timeSpan)}/{Map.currentLevel.metadata.totalLength}", color,
                Renderer.Alignment.Right, false, true);
        }
        private static void DrawEditorDifficulty(Vector2i position, Color color) => Core.renderer.DrawText(position,
            $"DIFFICULTY: {Map.currentLevel.metadata.displayDifficulty}", color,
            Alignment.Right, false, true);
        private static readonly Vector2i lastMaxComboPos = new Vector2i(2, 20);

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
        }*/
        public static void Draw() {
            /*switch(Game.menu) {
                case Menu.Settings:
                    DrawSettings();
                    break;
                case Menu.KeybindsEditor:
                    DrawKeybindsEditor();
                    break;
                case Menu.Game:
                    DrawGame();
                    break;
            }*/

            for(int i = 0; i < currentLayout.elements.Count; i++) {
                Element element = currentLayout.GetElement(i);
                
                element.Update();
                if(element.enabled) element.Draw();
            }

            Lua.Manager.DrawUI();
            
            if(Settings.GetBool("showFps"))
                Core.renderer.DrawText(fpsPos, $"{fps.ToString()}/{avgFPS.ToString()} FPS , {tps.ToString()} TPS",
                    fps >= 60 ? ColorScheme.GetColor("fps_good") : fps > 20 ? ColorScheme.GetColor("fps_ok") : 
                        ColorScheme.GetColor("fps_bad"), Alignment.Right);

            prevMousePosition = Core.renderer.mousePosition;
        }
        private static readonly Vector2i fpsPos = new Vector2i(79, 59);
    }
}
