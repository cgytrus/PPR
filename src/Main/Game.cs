using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using DiscordRPC;

using MoonSharp.Interpreter;

using NLog;

using PPR.GUI;
using PPR.GUI.Elements;
using PPR.Main.Levels;
using PPR.Main.Managers;
using PPR.Properties;

using PRR;

using SFML.Audio;
using SFML.Graphics;
using SFML.System;
using SFML.Window;

namespace PPR.Main {
    public enum StatsState { Pause, Fail, Pass }
    public enum SoundType { Hit, Hold, Fail, Pass, Click, Slider }

    public class Game {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public static StatsState statsState { get; private set; }
        
        public static Time levelTime { get; set; }
        private static float _offset;
        public static int roundedOffset { get; private set; }
        private static float _steps;
        public static float steps {
            get => _steps;
            private set {
                _steps = value;
                if(!editing) UI.progress = (int)(value / Map.currentLevel.metadata.maxStep * 80f);
            }
        }
        public static int roundedSteps { get; private set; }
        public static int currentDirectionLayer { get; private set; }
        public static int currentBPM { get; private set; } = 1;

        private static bool _playing;
        public static bool playing {
            get => _playing;
            set {
                if(value) SoundManager.PlayMusic();
                else SoundManager.PauseMusic();
                _playing = value;
            }
        }

        private static int _health = 80;
        public static int health {
            get => _health;
            set {
                value = Math.Clamp(value, 0, 80);
                _health = value;
                UI.health = value;
            }
        }

        public static bool editing { get; set; }
        public static bool auto { get; set; }
        public static bool changed { get; set; }
        public static bool exiting { get; private set; }
        public static float exitTime { get; set; }
        public static int menusAnimInitialOffset;
        public static List<LevelSpeed> menusAnimSpeeds;
        private static Time _prevLevelTime;
        private static Clock _tickClock = new Clock();
        private static float _prevSteps;
        private static float _absoluteCurrentSpeedSec = 60f;
        private static bool _usedAuto;
        private static float _tickAccumulator;
        private static int _tpsTicks;
        private static float _tpsTime;
        private static bool _prevLeftButtonPressed;
        private static bool _watchNegativeTime;
        public Game() {
            Settings.settingChanged += SettingChanged;
            Settings.Reload();
        }
        public static void Start() {
            RPC.Initialize();

            try {
                SoundManager.currentMusicPath = SoundManager.GetSoundFilePath(Path.Join("resources", "audio", Settings.GetPath("audio"), "mainMenu"));
                SoundManager.music = new Music(SoundManager.currentMusicPath);
            }
            catch(SFML.LoadingFailedException) {
                SoundManager.currentMusicPath = SoundManager.GetSoundFilePath(Path.Join("resources", "audio", "Default", "mainMenu"));
                SoundManager.music = new Music(SoundManager.currentMusicPath);
            }

            SoundManager.music.Volume = Settings.GetInt("musicVolume");
            SoundManager.PlayMusic();
            
            UI.RegenPositionRandoms();

            Lua.SendMessageToConsoles("onGameStart");
        }
        public static void Exit() {
            logger.Info("Exiting");
            
            Lua.SendMessageToConsoles("onGameExit");
            logger.Info("Sent shutdown event to Lua");

            SoundManager.StopMusic();
            logger.Info("Stopped music");

            Settings.SaveConfig();
            logger.Info("Saved settings");

            RPC.client.ClearPresence();
            RPC.client.Dispose();
            logger.Info("Removed Discord RPC");

            logger.Info("F to the logger");
            LogManager.Shutdown();

            exiting = true;
        }
        
        public static void UpdateSettings() {
            UI.LoadLayout(Path.Join("resources", "ui", "Default"));
            /*UI.musicVolumeSlider.value = Settings.GetInt("musicVolume");
            UI.soundsVolumeSlider.value = Settings.GetInt("soundsVolume");
            UI.bloomSwitch.selected = Settings.GetBool("bloom");
            UI.showFpsSwitch.selected = Settings.GetBool("showFps");
            UI.fullscreenSwitch.selected = Settings.GetBool("fullscreen");
            UI.fpsLimitSlider.value = Settings.GetInt("fpsLimit");
            UI.uppercaseSwitch.selected = Settings.GetBool("uppercaseNotes");*/

            LevelObject.linesColors = new Color[] {
                ColorScheme.GetColor("line_1_fg_color"), // 1234
                ColorScheme.GetColor("line_2_fg_color"), // qwer
                ColorScheme.GetColor("line_3_fg_color"), // asdf
                ColorScheme.GetColor("line_4_fg_color") // zxcv
            };
            LevelObject.linesDarkColors = new Color[] {
                ColorScheme.GetColor("line_1_bg_color"), // 1234
                ColorScheme.GetColor("line_2_bg_color"), // qwer
                ColorScheme.GetColor("line_3_bg_color"), // asdf
                ColorScheme.GetColor("line_4_bg_color") // zxcv
            };
            
            LevelSpeedObject.speedColor = ColorScheme.GetColor("speed");
            LevelSpeedObject.nextDirLayerSpeedColor = ColorScheme.GetColor("next_dir_layer_speed");
        }
        private static void SettingChanged(object caller, SettingChangedEventArgs e) {
            switch (e.settingName) {
                case "font": {
                    string[] fontMappingsLines = File.ReadAllLines(Path.Join("resources", "fonts",
                        Settings.GetPath("font"), "mappings.txt"));
                    string[] fontSizeStr = fontMappingsLines[0].Split(',');
                    Core.renderer.fontSize = new Vector2i(int.Parse(fontSizeStr[0]), int.Parse(fontSizeStr[1]));
                    Core.renderer.windowWidth = Core.renderer.width * Core.renderer.fontSize.X;
                    Core.renderer.windowHeight = Core.renderer.height * Core.renderer.fontSize.Y;

                    BitmapFont font = new BitmapFont(
                        new Image(Path.Join("resources", "fonts", Settings.GetPath("font"), "font.png")),
                        fontMappingsLines[1], Core.renderer.fontSize);
                    Core.renderer.text = new BitmapText(font, new Vector2i(Core.renderer.width, Core.renderer.height)) {
                        text = Core.renderer.display
                    };

                    Core.renderer.UpdateWindow();
                    break;
                }
                case "colorScheme": ColorScheme.Reload();
                    break;
                case "fullscreen": Core.renderer.SetFullscreen(Settings.GetBool("fullscreen"));
                    break;
                case "musicVolume":
                    SoundManager.music.Volume = Settings.GetInt("musicVolume");
                    break;
                case "soundsVolume":
                    SoundManager.UpdateSoundsVolume();
                    break;
                case "audio":
                    SoundManager.ReloadSounds();
                    break;
                case "fpsLimit":
                    Core.renderer.UpdateFramerateSetting();
                    break;
            }
        }
        
        public static void LostFocus(object caller, EventArgs args) {
            //if(menu == Menu.Game) menu = Menu.LastStats;
            SoundManager.music.Volume = 0;
            Core.renderer.UpdateFramerateSetting();
        }
        public static void GainedFocus(object caller, EventArgs args) {
            SoundManager.music.Volume = Settings.GetInt("musicVolume");
            Core.renderer.UpdateFramerateSetting();
        }
        
        public static void StartGame(string musicPath) {
            // Reset everything when we enter the level or bad things will happen

            statsState = StatsState.Pause;
            _usedAuto = auto;
            UI.progress = 80;
            _offset = 0;
            roundedOffset = 0;
            steps = 0;
            roundedSteps = 0;
            _prevSteps = 0;
            currentDirectionLayer = 0;
            currentBPM = Map.currentLevel.speeds[0].speed;
            levelTime = Time.Zero;
            _prevLevelTime = Time.Zero;
            playing = false;
            UI.health = 0;
            health = 80;
            _tickAccumulator = 0f;
            _tpsTicks = 0;
            _tpsTime = 0f;
            _prevLeftButtonPressed = false;
            LevelNote.closestSteps.Clear();
            ScoreManager.ResetScore();
            SoundManager.StopMusic();

            if(Map.currentLevel.script != null) Lua.TryLoadScript(Map.currentLevel.script);

            // Load music
            if(File.Exists(musicPath)) {
                SoundManager.currentMusicPath = musicPath;
                SoundManager.music = new Music(musicPath) {
                    Volume = Settings.GetInt("musicVolume"),
                    PlayingOffset = Time.FromMilliseconds(Map.currentLevel.metadata.musicOffset)
                };
                if(!editing) _playing = true;
            }
            
            _tickClock.Restart();

            logger.Info("Entered level '{0}' by {1}", Map.currentLevel.metadata.name, Map.currentLevel.metadata.author);
        }

        public static void UpdatePresence() {
            string lvlName = Map.currentLevel == null ? "this" : Map.currentLevel.metadata.name;
            string lvlDiff = Map.currentLevel == null ? "doesn't" : Map.currentLevel.metadata.displayDiff;
            string lvlDifficulty = Map.currentLevel == null ? "matter" : Map.currentLevel.metadata.displayDifficulty;
            // anyway
            
            Time useTimeFromStart = SoundManager.music.PlayingOffset -
                (Map.currentLevel == null ? Time.Zero : Time.FromMilliseconds(Map.currentLevel.metadata.musicOffset));

            if(UI.currentLayout.IsElementEnabled("mainMenu")) {
                RPC.SetPresence("In main menu");
            }
            else if(UI.currentLayout.IsElementEnabled("levelSelect")) {
                RPC.SetPresence($"Selecting a level to {(editing ? "edit" : "play")}");
            }
            else if(UI.currentLayout.IsElementEnabled("game")) {
                RPC.SetPresence(editing ? "Editing" :
                    auto ? "Watching" : "Playing",
                    Map.currentLevel == null ? "wtf how do you even see this ???/?//" :
                        $"{lvlName} [{lvlDiff} ({lvlDifficulty})]", "",
                    Map.currentLevel == null || editing ? null : Timestamps.FromTimeSpan(
                        Map.currentLevel.metadata.lengthSpan -
                        TimeSpan.FromMilliseconds(useTimeFromStart.AsMilliseconds())));
            }
            else if(UI.currentLayout.IsElementEnabled("lastStats")) {
                RPC.SetPresence(editing ? "Paused Editing" :
                    statsState == StatsState.Pause ? "Paused" :
                    statsState == StatsState.Pass ? "Passed" : "Failed",
                    Map.currentLevel == null ? "wtf how do you even see this ???/?//" :
                        $"{lvlName} [{lvlDiff} ({lvlDifficulty})]");
            }
        }
        
        public void Update() {
            if(exiting) exitTime -= Core.deltaTime;
            
            if(UI.currentLayout.IsElementEnabled("mainMenu") && SoundManager.music.Status == SoundStatus.Stopped)
                SoundManager.SwitchMusic();
            
            // Update the menus background animation BPM
            if(!UI.currentLayout.IsElementEnabled("game")) {
                if(Path.GetFileName(Path.GetDirectoryName(SoundManager.currentMusicPath)) == "Default" ||
                   SoundManager.music.Status != SoundStatus.Playing) UI.menusAnimBPM = 60;
                else {
                    int step = (int)Calc.MillisecondsToSteps(
                        SoundManager.music.PlayingOffset.AsMilliseconds() - menusAnimInitialOffset, menusAnimSpeeds);
                    UI.menusAnimBPM = Math.Abs(Calc.GetBPMAtStep(step, menusAnimSpeeds));
                }

                UI.tps = 0;
                return; // Everything after this line will be executed only if the player is playing a level
            }

            if(statsState != StatsState.Pause) return;

            float speedMultiplier = SoundManager.music.Pitch;

            // Execute the ticks
            float fixedDeltaTime = _absoluteCurrentSpeedSec / 16f / speedMultiplier;

            _tickAccumulator += _tickClock.Restart().AsSeconds() * speedMultiplier;
            while(_tickAccumulator >= fixedDeltaTime) {
                if(playing) levelTime += Time.FromSeconds(fixedDeltaTime);
                Tick();
                _tickAccumulator -= fixedDeltaTime;
                ++_tpsTicks;
            }
            
            if(_tpsTime >= 1f) {
                UI.tps = (int)(_tpsTicks / _tpsTime);
                _tpsTicks = 0;
                _tpsTime = 0f;
            }
            _tpsTime += Core.deltaTime;

            Lua.Update();

            if(_watchNegativeTime || !playing || editing || SoundManager.music.Status == SoundStatus.Playing) return;
            UpdateMusicTime();
            playing = true;
        }
        private static void Tick() {
            if(_watchNegativeTime && playing) {
                _watchNegativeTime = false;
                UpdateMusicTime();
                if(!_watchNegativeTime) SoundManager.PlayMusic();
            }
            
            #region Update steps and offset

            if(levelTime != _prevLevelTime) {
                steps = Calc.MillisecondsToSteps(levelTime.AsMicroseconds() / 1000f);
                if(SoundManager.music.Status != SoundStatus.Playing) steps = MathF.Round(steps);
                _offset = Calc.StepsToOffset(steps);
            }
            _prevLevelTime = levelTime;

            #endregion

            //if(steps - prevSteps > 1f)
            //    logger.Warn("Lag detected: steps increased too quickly ({0})", steps - prevSteps);

            if(MathF.Floor(_prevSteps) != MathF.Floor(steps)) {
                roundedSteps = (int)MathF.Round(steps);
                roundedOffset = (int)MathF.Round(_offset);
                RecalculatePosition();
            }
            _prevSteps = steps;

            if(editing) {
                float initialOffset = Map.currentLevel.metadata.musicOffset / 1000f;
                float duration = SoundManager.music.Duration.AsSeconds() + initialOffset;
                
                if(Core.renderer.mousePosition.Y == 0) {
                    if(Core.renderer.leftButtonPressed) { // Is pressed
                        if(!_prevLeftButtonPressed) SoundManager.PauseMusic(); // Just pressed
                        float mouseProgress = Math.Clamp(Core.renderer.mousePositionF.X / 80f, 0f, 1f);
                        levelTime = Time.FromSeconds(duration * mouseProgress);
                        steps = MathF.Round(Calc.MillisecondsToSteps(levelTime.AsMilliseconds()));
                        _offset = Calc.StepsToOffset(steps);
                    }
                    else if(_prevLeftButtonPressed) { // Just released
                        UpdateMusicTime();
                        if(playing) SoundManager.PlayMusic();
                    }
                }

                UI.progress = (int)(levelTime.AsSeconds() / duration * 80f);
                _prevLeftButtonPressed = Core.renderer.leftButtonPressed;
            }

            statsState = health > 0 ? Map.currentLevel.objects.Count > 0 ? StatsState.Pause : StatsState.Pass :
                StatsState.Fail;

            Map.TickAll();

            Lua.Tick();

            if(statsState != StatsState.Pause) Lua.SendMessageToConsoles("onPassOrFail");
        }
        
        public static void RoundSteps() => steps = roundedSteps;
        
        public static void UpdateTime() {
            long useMicrosecs = (long)(MathF.Round(Calc.StepsToMilliseconds(steps)) * 1000f);
            levelTime = Time.FromMicroseconds(useMicrosecs);
        }

        public static void UpdateMusicTime() {
            Time playingOffset = levelTime + Time.FromMilliseconds(Map.currentLevel.metadata.musicOffset);
            if(playingOffset < Time.Zero) _watchNegativeTime = true;
            SoundManager.music.PlayingOffset = playingOffset;
        }

        private static void UpdateSpeeds() {
            foreach (LevelSpeed speed in Map.currentLevel.speeds)
                if(speed.step <= steps) {
                    currentBPM = speed.speed;
                    _absoluteCurrentSpeedSec = Math.Abs(60f / currentBPM);
                    currentDirectionLayer = Calc.StepsToDirectionLayer(roundedSteps);
                }
                else break;
        }
        public static void RecalculatePosition() {
            UpdateSpeeds();
            Map.StepAll();
        }

        #region Unfoldable (for some reason) fricking 50 lines long switch (key -> char bindings), thanks, Rider

        public static char GetNoteBinding(Keyboard.Key key) => key switch
        {
            Keyboard.Key.Num1 => '1',
            Keyboard.Key.Num2 => '2',
            Keyboard.Key.Num3 => '3',
            Keyboard.Key.Num4 => '4',
            Keyboard.Key.Num5 => '5',
            Keyboard.Key.Num6 => '6',
            Keyboard.Key.Num7 => '7',
            Keyboard.Key.Num8 => '8',
            Keyboard.Key.Num9 => '9',
            Keyboard.Key.Num0 => '0',
            Keyboard.Key.Hyphen => '-',
            Keyboard.Key.Equal => '=',
            Keyboard.Key.Q => 'q',
            Keyboard.Key.W => 'w',
            Keyboard.Key.E => 'e',
            Keyboard.Key.R => 'r',
            Keyboard.Key.T => 't',
            Keyboard.Key.Y => 'y',
            Keyboard.Key.U => 'u',
            Keyboard.Key.I => 'i',
            Keyboard.Key.O => 'o',
            Keyboard.Key.P => 'p',
            Keyboard.Key.LBracket => '[',
            Keyboard.Key.RBracket => ']',
            Keyboard.Key.A => 'a',
            Keyboard.Key.S => 's',
            Keyboard.Key.D => 'd',
            Keyboard.Key.F => 'f',
            Keyboard.Key.G => 'g',
            Keyboard.Key.H => 'h',
            Keyboard.Key.J => 'j',
            Keyboard.Key.K => 'k',
            Keyboard.Key.L => 'l',
            Keyboard.Key.Semicolon => ';',
            Keyboard.Key.Quote => '\'',
            Keyboard.Key.Z => 'z',
            Keyboard.Key.X => 'x',
            Keyboard.Key.C => 'c',
            Keyboard.Key.V => 'v',
            Keyboard.Key.B => 'b',
            Keyboard.Key.N => 'n',
            Keyboard.Key.M => 'm',
            Keyboard.Key.Comma => ',',
            Keyboard.Key.Period => '.',
            Keyboard.Key.Slash => '/',
            _ => '\0'
        };

        #endregion

        private static void ChangeSpeed(int delta) {
            // Create a new speed if we don't have a speed at the current position
            List<int> flooredSpeedsSteps = Map.currentLevel.speeds.Select(speed => speed.step).ToList();
            if(!flooredSpeedsSteps.Contains((int)steps)) {
                int speedIndex = 0;
                for(int i = 0; i < Map.currentLevel.speeds.Count; i++)
                    if(Map.currentLevel.speeds[i].step <= steps) speedIndex = i;
                Map.currentLevel.speeds.Add(new LevelSpeed(Map.currentLevel.speeds[speedIndex].speed, (int)steps));
                Map.currentLevel.speeds.Sort((speed1, speed2) => speed1.step.CompareTo(speed2.step));
            }

            // Get the index of the speed we want to change
            flooredSpeedsSteps = Map.currentLevel.speeds.Select(speed => speed.step).ToList();
            int index = flooredSpeedsSteps.IndexOf((int)steps);

            Map.currentLevel.speeds[index].speed += delta;

            #region Remove redundant speeds

            // Remove the next speed if it's the same as the current one
            if(index < Map.currentLevel.speeds.Count - 1 &&
               Map.currentLevel.speeds[index].speed == Map.currentLevel.speeds[index + 1].speed)
                Map.currentLevel.speeds.RemoveAt(index + 1);
            
            // Remove the current speed if it's the same as the previous one
            if(index >= 1 && Map.currentLevel.speeds[index].speed == Map.currentLevel.speeds[index - 1].speed)
                Map.currentLevel.speeds.RemoveAt(index);

            #endregion

            // Recreate the objects that show the speeds
            List<LevelObject> speedObjects = Map.currentLevel.objects.FindAll(obj => obj is LevelSpeedObject);
            foreach(LevelObject obj in speedObjects) obj.remove = RemoveType.NoAnimation;
            foreach(LevelSpeed speed in Map.currentLevel.speeds)
                Map.currentLevel.objects.Add(new LevelSpeedObject(speed.step, Map.currentLevel.speeds));
            
            changed = true;
        }
        
        public static void KeyPressed(object caller, KeyEventArgs key) {
            // Back
            /*if(Bindings.GetBinding("back").IsPressed(key) &&
               !UI.currentLayouts.Contains("levelSelect") && !UI.currentLayouts.Contains("lastStats")) {
                menu = menu switch {
                    Menu.Game => Menu.LastStats,
                    Menu.KeybindsEditor => Menu.Settings,
                    _ => Menu.Main
                };
                Lua.SendMessageToConsoles("onGoBack");
            }*/
            
            // Fullscreen
            if(Bindings.GetBinding("fullscreen").IsPressed(key))
                Settings.SetBool("fullscreen", !Settings.GetBool("fullscreen"));
            
            if(!UI.currentLayout.IsElementEnabled("game")) return;
            
            char character = GetNoteBinding(key.Code);

            if(!editing) {
                if(auto || character == '\0') return;
                Map.flashLine = Calc.GetXPosForCharacter(character);
                return;
            }
            // Handle keybinds
            if(character == '\0' || key.Control) {
                #region Erase

                if(Bindings.GetBinding("erase").IsPressed(key)) { if(Map.Erase()) SetChanged(); }

                #endregion

                #region Cut/Copy/Paste

                else if(Bindings.GetBinding("cut").IsPressed(key)) { if(Map.Cut()) SetChanged(); }
                else if(Bindings.GetBinding("copy").IsPressed(key)) Map.Copy();
                else if(Bindings.GetBinding("paste").IsPressed(key)) { if(Map.Paste()) SetChanged(); }

                #endregion

                #region Move

                else if(Bindings.GetBinding("moveUp").IsPressed(key)) {
                    if(Map.MoveVertical(1)) changed = true;
                }
                else if(Bindings.GetBinding("moveDown").IsPressed(key)) {
                    if(Map.MoveVertical(-1)) changed = true;
                }
                else if(Bindings.GetBinding("moveUpFast").IsPressed(key)) {
                    if(Map.MoveVertical(10)) changed = true;
                }
                else if(Bindings.GetBinding("moveDownFast").IsPressed(key)) {
                    if(Map.MoveVertical(-10)) changed = true;
                }
                else if(Bindings.GetBinding("moveLeft").IsPressed(key)) {
                    if(Map.MoveHorizontal(-1)) changed = true;
                }
                else if(Bindings.GetBinding("moveRight").IsPressed(key)) {
                    if(Map.MoveHorizontal(1)) changed = true;
                }

                #endregion

                #region Lines

                else if(Bindings.GetBinding("linesFrequencyUp").IsPressed(key)) {
                    Map.currentLevel.metadata.linesFrequency++;
                    changed = true;
                }
                else if(Bindings.GetBinding("linesFrequencyDown").IsPressed(key)) {
                    Map.currentLevel.metadata.linesFrequency--;
                    changed = true;
                }

                #endregion

                #region Speed

                else if(Bindings.GetBinding("speedUpSlow").IsPressed(key)) ChangeSpeed(1);
                else if(Bindings.GetBinding("speedDownSlow").IsPressed(key)) ChangeSpeed(-1);
                else if(Bindings.GetBinding("speedUp").IsPressed(key)) ChangeSpeed(10);
                else if(Bindings.GetBinding("speedDown").IsPressed(key)) ChangeSpeed(-10);

                #endregion

                #region Fast scroll

                else if(Bindings.GetBinding("fastScrollUp").IsPressed(key)) ScrollTime(10);
                else if(Bindings.GetBinding("fastScrollDown").IsPressed(key)) ScrollTime(-10);

                #endregion
            }
            // Handle placing/removing individual notes
            else {
                // Removing individual notes
                if(key.Alt) {
                    IEnumerable<LevelObject> objects = Map.currentLevel.notes.Where(obj =>
                        (Map.selecting ? Map.OffsetSelected(Calc.StepsToOffset(obj.step)) :
                            obj.step == (int)steps) && obj.key == key.Code);
                        
                    foreach(LevelObject obj in objects) {
                        obj.remove = RemoveType.NoAnimation;
                            
                        changed = true;
                        Map.selecting = false;
                    }
                }
                // Placing notes
                else {
                    List<RecreatableLevelNote> toCreate = new List<RecreatableLevelNote>();
                        
                    Func<char, int, IEnumerable<LevelSpeed>, LevelNote> constructor = (nChar, step, speeds) =>
                        new LevelHitNote(nChar, step, speeds);
                    if(key.Shift)
                        constructor = (nChar, step, speeds) => new LevelHoldNote(nChar, step, speeds);
                        
                    if(Map.selecting)
                        for(int i = Map.selectionStart; i <= Map.selectionEnd; i++) {
                            float step = Calc.OffsetToSteps(i, currentDirectionLayer);
                            if(float.IsNaN(step) || Calc.StepsToDirectionLayer(step) != currentDirectionLayer)
                                continue;
                            foreach(LevelNote note in Map.currentLevel.notes.Where(obj =>
                                obj.step == step && obj.key == key.Code)) note.remove = RemoveType.NoAnimation;
                            toCreate.Add(new RecreatableLevelNote(character, (int)step, constructor));
                        }
                    else {
                        foreach(LevelNote note in Map.currentLevel.notes.Where(obj =>
                            obj.step == (int)steps && obj.key == key.Code)) note.remove = RemoveType.NoAnimation;
                        toCreate.Add(new RecreatableLevelNote(character, (int)steps, constructor));
                    }

                    foreach(RecreatableLevelNote obj in toCreate) {
                        Map.currentLevel.objects.Add(obj.constructor(obj.character, obj.step,
                            Map.currentLevel.speeds));
                    }

                    if(toCreate.Count > 0) {
                        changed = true;
                        if(!Core.renderer.leftButtonPressed) Map.selecting = false;
                    }
                }
            }

            RecalculatePosition();

            if(!changed) return;

            List<LightLevelObject> objs = Calc.ObjectsToLightObjects(Map.currentLevel.objects);
                
            Map.currentLevel.metadata.lengthSpan = Calc.GetLevelLength(objs, Map.currentLevel.speeds,
                Map.currentLevel.metadata.musicOffset);
            Map.currentLevel.metadata.length = Calc.TimeSpanToLength(Map.currentLevel.metadata.lengthSpan);
                
            Map.currentLevel.metadata.totalLength = Calc.TimeSpanToLength(
                Calc.GetTotalLevelLength(objs, Map.currentLevel.speeds, Map.currentLevel.metadata.musicOffset));
                
            Map.currentLevel.metadata.difficulty = Calc.GetDifficulty(Map.currentLevel.objects,
                Map.currentLevel.speeds, (int)Map.currentLevel.metadata.lengthSpan.TotalMinutes);
                
            Map.currentLevel.metadata.maxStep = Calc.GetLastObject(Map.currentLevel.notes)?.step ?? 0;
        }
        private static void SetChanged() {
            changed = true;
            Map.selecting = false;
        }

        public static void MouseWheelScrolled(object caller, MouseWheelScrollEventArgs scroll) {
            /*if(UI.currentLayout.IsElementEnabled("levelSelect")) {
                Vector2i mousePos = Core.renderer.mousePosition;
                if(mousePos.Y >= 12 && mousePos.Y <= 49) {
                    if(mousePos.X >= 28 && mousePos.X <= 51) {
                        // Scroll Levels
                        if(mousePos.Y <= 38) {
                            if(scroll.Delta > 0 && UI.levelSelectLevels.First().Value.button.position.Y >= 12) return;
                            if(scroll.Delta < 0 && UI.levelSelectLevels.Last().Value.button.position.Y <= 38) return;
                            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
                            foreach(LevelSelectLevel level in UI.levelSelectLevels.Values)
                                level.button.position += new Vector2i(0, (int)scroll.Delta);
                        }
                        // Scrolls Diffs
                        else if(mousePos.Y >= 40) {
                            IOrderedEnumerable<KeyValuePair<string, LevelSelectDiff>> sortedDiffs =
                                UI.levelSelectLevels[UI.currSelectedLevel].diffs
                                    .OrderBy(pair => pair.Value.metadata.difficulty);
                            if(scroll.Delta > 0 && sortedDiffs.First().Value.button.position.Y >= 40) return;
                            if(scroll.Delta < 0 && sortedDiffs.Last().Value.button.position.Y <= 49) return;
                            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
                            foreach(LevelSelectDiff diff in UI.levelSelectLevels[UI.currSelectedLevel].diffs.Values)
                                diff.button.position += new Vector2i(0, (int)scroll.Delta);
                        }
                    }
                    // Scroll Scores
                    else if(mousePos.X >= 1 && mousePos.X <= 25 && !string.IsNullOrEmpty(UI.currSelectedLevel) &&
                            !string.IsNullOrEmpty(UI.currSelectedDiff)) {
                        LevelSelectDiff currentDiff =
                            UI.levelSelectLevels[UI.currSelectedLevel].diffs[UI.currSelectedDiff];
                        if(currentDiff.scores != null && currentDiff.scores.Count > 0) {
                            if(scroll.Delta > 0 && currentDiff.scores.First().scorePosition.Y >= 12) return;
                            if(scroll.Delta < 0 && currentDiff.scores.Last().scoresPosition.Y <= 49) return;
                            for(int i = 0; i < currentDiff.scores.Count; i++)
                                currentDiff.scores[i].Move(new Vector2i(0, (int)scroll.Delta));
                        }
                    }
                }
            }
            else */if(editing && UI.currentLayout.IsElementEnabled("game")) ScrollTime((int)scroll.Delta);
        }

        public static void ScrollTime(int delta) {
            if(playing) playing = false;
            steps = Math.Clamp(steps + delta, 0, Calc.MillisecondsToSteps(SoundManager.music.Duration.AsMicroseconds() / 1000f));
            UpdateTime();
        }
        
        public static bool StepPassedLine(int step, int lineOffset = 0) => roundedSteps >= step + lineOffset;

        public static int GetInitialOffset(IReadOnlyList<string> lines) {
            string[] meta = lines.Count > 4 ? lines[4].Split(':') : Array.Empty<string>();
            return meta.Length > 5 ? int.Parse(meta[5]) : 0;
        }
        public static List<LevelSpeed> GetSpeeds(IReadOnlyList<string> lines) {
            int[] speeds = lines.Count > 2 && lines[2].Length > 0 ? lines[2].Split(':').Select(int.Parse).ToArray() :
                Array.Empty<int>();
            int[] speedsStarts = lines.Count > 3 && lines[3].Length > 0 ?
                lines[3].Split(':').Select(int.Parse).ToArray() : Array.Empty<int>();
            List<LevelSpeed> levelSpeeds = speedsStarts.Select((step, i) => new LevelSpeed(speeds[i], step)).ToList();
            levelSpeeds.Sort((speed1, speed2) => speed1.step.CompareTo(speed2.step));
            return levelSpeeds;
        }

        private static void RescanCreatedLevels() {
            string[] infoFiles = Directory.GetFiles("levels").Where(file => Path.GetExtension(file) == ".txt").ToArray();
            foreach(string infoFile in infoFiles) {
                string musicPath = SoundManager.GetSoundFilePath(Path.Join(Path.GetDirectoryName(infoFile),
                    Path.GetFileNameWithoutExtension(infoFile)));
                string levelName = Path.GetFileNameWithoutExtension(infoFile);
                
                if(musicPath == "" && !Directory.Exists(Path.Join("levels", levelName))) continue;
                
                string musicExtension = Path.GetExtension(musicPath);
                string[] diffsStr = File.ReadAllLines(infoFile);
                string levelFolder = Directory.CreateDirectory(Path.Join("levels", levelName)).FullName;
                
                if(musicPath != "") File.Move(musicPath, Path.Join(levelFolder, $"music{musicExtension}"));
                
                foreach(string diffStr in diffsStr) {
                    // Format the diff info
                    string[] diffInfo = diffStr.Split(':');
                    
                    // Save the diff info
                    string diffName = diffInfo[0].ToLowerInvariant();
                    string diffAuthor = diffInfo.Length > 1 ? diffInfo[1] : "";
                    string newDiffName = diffInfo.Length > 2 && diffInfo[2] != "" ? diffInfo[2] : diffName;

                    string diffPath = Path.Join(levelFolder, $"{diffName}.txt");
                    string newDiffPath = Path.Join(levelFolder, $"{newDiffName}.txt");

                    bool canUpdate = File.Exists(diffPath);
                    bool updatingAuthor = canUpdate && diffAuthor != "";
                    bool updatingName = canUpdate && newDiffName != diffName;
                    bool updating = updatingAuthor || updatingName;
                    
                    // Copy the template level
                    string[] levelLines = updating ?
                        File.ReadAllLines(diffPath) :
                        File.ReadAllLines(Path.Join("levels", "_template", "level.txt"));
                    
                    // Change the level author
                    string[] meta = levelLines[4].Split(':');
                    meta[3] = diffAuthor == "" ? meta[3] : diffAuthor;
                    levelLines[4] = string.Join(':', meta);
                    
                    // Save the diff to the level folder
                    File.WriteAllLines(newDiffPath, levelLines);
                    
                    // Delete the old diff file if we're updating the level name
                    if(updatingName) File.Delete(diffPath);
                }
                
                File.Delete(infoFile);
            }
        }
        
        public static void GenerateLevelList() {
            foreach((string id, UIElement _) in UI.currentLayout.elements.Where(
                elem => elem.Value.parent?.id == "levelSelect.levels" ||
                        elem.Value.parent?.id == "levelSelect.difficulties" ||
                        elem.Value.parent?.id == "levelSelect.scores" ||
                        elem.Value.parent?.id == "levelSelect.metadatas")) {
                UI.currentLayout.RemoveElement(id);
            }

            RescanCreatedLevels();
            
            string[] directories = Directory.GetDirectories("levels")
                .Where(path => Path.GetFileName(path) != "_template").ToArray();
            UI.levelSelectLevels = new Dictionary<string, LevelSelectLevel>(directories.Length);
            for(int i = 0; i < directories.Length; i++) {
                string levelName = Path.GetFileName(directories[i]);
                Lua.SendMessageToConsoles("generateLevelSelectLevelButton", DynValue.NewNumber(i),
                    DynValue.NewString(levelName));
                LevelSelectLevel level = new LevelSelectLevel {
                    button = (Button)UI.currentLayout.elements[$"levelSelect.levels.level.{levelName}"]
                };

                #region Load Diffs

                string[] diffFiles = Directory.GetFiles(directories[i]).Where(file => file.EndsWith(".txt")).ToArray();
                level.diffs = new Dictionary<string, LevelSelectDiff>(diffFiles.Length);
                foreach(string diffFile in diffFiles) {
                    string diffName = Path.GetFileNameWithoutExtension(diffFile);
                    string[] diffLines = File.ReadAllLines(Path.Join(directories[i], $"{diffName}.txt"));
                    if(!Level.IsLevelValid(diffLines) || diffName == null ||
                       diffName != diffName.ToLowerInvariant()) continue;
                    LevelMetadata metadata = new LevelMetadata(diffLines, levelName, diffName);
                    LevelSelectDiff diff = new LevelSelectDiff {
                        metadata = metadata
                    };
                    
                    logger.Info("Loaded metadata for level '{0}' diff '{1}'", levelName, diffName);

                    #region Load Scores

                    string scoresPath = diffName == "level" ? Path.Join("scores", $"{levelName}.txt") :
                        Path.Join("scores", $"{levelName}", $"{diffName}.txt");
                    diff.scores = Map.ScoresFromLines(File.Exists(scoresPath) ? File.ReadAllLines(scoresPath) :
                        Array.Empty<string>());

                    #endregion
                    
                    logger.Info("Loaded scores for level '{0}' diff '{1}', total scores count: {2}",
                        levelName, diffName, diff.scores?.Count);
                    
                    level.diffs.Add(diffName, diff);
                }
                List<KeyValuePair<string, LevelSelectDiff>> sortedDiffs =
                    level.diffs.OrderBy(pair => pair.Value.metadata.difficulty).ToList();
                for(int j = 0; j < sortedDiffs.Count; j++) {
                    Lua.SendMessageToConsoles("generateLevelSelectDifficultyButton", DynValue.NewNumber(j),
                        DynValue.NewString(levelName),
                        DynValue.NewString(sortedDiffs[j].Key),
                        DynValue.NewString(level.diffs[sortedDiffs[j].Key].metadata.displayDifficulty));
                    LevelSelectDiff diff = level.diffs[sortedDiffs[j].Key];
                    diff.button = (Button)UI.currentLayout
                        .elements[$"levelSelect.difficulties.{levelName}.difficulty.{sortedDiffs[j].Key}"];
                    level.diffs[sortedDiffs[j].Key] = diff;
                }

                #endregion

                logger.Info("Loaded diffs for level '{0}', total diffs count: {1}", levelName, level.diffs.Count);
                
                UI.levelSelectLevels.Add(levelName, level);

                foreach((string difficultyName, LevelSelectDiff _) in sortedDiffs) {
                    Lua.SendMessageToConsoles("generateLevelSelectMetadata", DynValue.NewString(levelName),
                        DynValue.NewString(difficultyName));
                    Lua.SendMessageToConsoles("generateLevelSelectScores", DynValue.NewString(levelName),
                        DynValue.NewString(difficultyName));
                }
            }

            logger.Info("Loaded levels, total level count: {0}", UI.levelSelectLevels.Count);
        }
    }
}
