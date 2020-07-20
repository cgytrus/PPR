﻿using System;
using System.Collections.Generic;
using System.Linq;

using PPR.GUI;
using PPR.Rendering;

using SFML.Audio;
using SFML.Graphics;
using SFML.Window;

namespace PPR.Main.Levels {
    public struct LevelScore {
        public Vector2 scorePosition;
        public int score;
        public string scoreStr;
        public Vector2 accComboPosition;
        public int accuracy;
        public int maxCombo;
        public string accuracyStr;
        public Color accuracyColor;
        public Vector2 accComboDividerPosition;
        public Vector2 maxComboPosition;
        public string maxComboStr;
        public Color maxComboColor;
        public Vector2 scoresPosition;
        public int[] scores;
        public Vector2 linePosition;

        public LevelScore(Vector2 position, int score, int accuracy, int maxCombo, int[] scores) {
            scorePosition = position;
            this.score = score;
            scoreStr = "SCORE: " + score;
            accComboPosition = new Vector2(position.x, position.y + 1);
            this.accuracy = accuracy;
            this.maxCombo = maxCombo;
            accuracyStr = accuracy + "%";
            accuracyColor = Game.GetAccuracyColor(accuracy);
            accComboDividerPosition = accComboPosition + new Vector2(accuracyStr.Length, 0);
            maxComboPosition = accComboDividerPosition + new Vector2(1, 0);
            maxComboStr = maxCombo + "x";
            maxComboColor = Game.GetComboColor(accuracy, scores[0]);
            scoresPosition = new Vector2(position.x, position.y + 2);
            this.scores = scores;
            linePosition = new Vector2(position.x - 1, position.y + 3);
        }
    }
    public struct LevelMetadata {
        public string name;
        public int hpDrain;
        public int hpRestorage;
        public string difficulty;
        public string author;
        public string length;
        public int maxStep;
        public int minBPM;
        public int maxBPM;
        public int avgBPM;
        public int linesFrequency;
        public int initialOffsetMS;
        public string bpm;
        public readonly bool skippable;
        public readonly int skipTime;
        public readonly int objectCount;
        public readonly int speedsCount;

        public LevelMetadata(string name, string[] meta, int objectCount, List<int> steps, List<LevelSpeed> speeds) {
            this.name = name;
            hpDrain = int.Parse(meta[0]);
            hpRestorage = int.Parse(meta[1]);
            difficulty = meta[2];
            author = meta[3];
            linesFrequency = meta.Length > 4 ? int.Parse(meta[4]) : 4;
            initialOffsetMS = meta.Length > 5 ? int.Parse(meta[5]) : 0;

            //speeds = Game.SortLevelSpeeds(speeds);
            speeds.Sort((speed1, speed2) => speed1.step.CompareTo(speed2.step));

            maxStep = steps.Count > 0 ? steps.Max() : 0;
            TimeSpan timeSpan = TimeSpan.FromMilliseconds(Game.StepsToMilliseconds(maxStep, speeds) - initialOffsetMS);
            length = (timeSpan < TimeSpan.Zero ? "-" : "") + timeSpan.ToString((timeSpan.Hours != 0 ? "h':'mm" : "m") + "':'ss");

            int minStep = steps.Count > 0 ? steps.Min() : 0;
            int minTime = (int)Game.StepsToMilliseconds(minStep, speeds) + initialOffsetMS;
            skipTime = minTime - 3000;
            skippable = skipTime > 3000;

            IEnumerable<int> onlySpeeds = speeds.Select(speed => speed.speed);
            minBPM = onlySpeeds.Min();
            maxBPM = onlySpeeds.Max();
            avgBPM = (int)Math.Floor(onlySpeeds.Average());
            string avgBPMstr = avgBPM.ToString();
            string minmaxBPMstr = minBPM + "-" + maxBPM;
            bpm = minBPM == maxBPM ? avgBPMstr : minmaxBPMstr + " (" + avgBPMstr + ")";

            this.objectCount = objectCount;
            speedsCount = speeds.Count;
        }
        public LevelMetadata(Level level, string[] meta, string name) : this(name, meta,
            level.objects.FindAll(obj => obj.character != LevelObject.holdChar).Count,
            level.objects.FindAll(obj => obj.character != LevelObject.speedChar).Select(obj => obj.step).ToList(), level.speeds) { }
        static List<LevelSpeed> SpeedsFromLists(List<int> speeds, List<int> speedsSteps) {
            List<LevelSpeed> combinedSpeeds = new List<LevelSpeed>();
            for(int i = 0; i < speedsSteps.Count; i++) {
                combinedSpeeds.Add(new LevelSpeed(speeds[i], speedsSteps[i]));
            }
            combinedSpeeds.Sort((speed1, speed2) => speed1.step.CompareTo(speed2.step));
            return combinedSpeeds;
        }
        public LevelMetadata(string[] lines, string name) : this(name, lines[4].Split(':'),
                                                                                                                                  lines[0].ToList().FindAll(obj => obj != LevelObject.holdChar).Count,
                                                                                                                                  lines[1].Length > 0 ? lines[1].Split(':').Select(str => int.Parse(str)).ToList() : new List<int>(),
                                                                                      SpeedsFromLists(lines[2].Length > 0 ? lines[2].Split(':').Select(str => int.Parse(str)).ToList() : new List<int>(),
                                                                                                                        lines[3].Length > 0 ? lines[3].Split(':').Select(str => int.Parse(str)).ToList() : new List<int>())) { }
    }
    public class Level {
        public LevelMetadata metadata;
        public List<LevelObject> objects = new List<LevelObject>();
        public List<LevelSpeed> speeds = new List<LevelSpeed>();

        public Level(string[] lines, string name) {
            int[] objectsSteps = lines[1].Length > 0 ? lines[1].Split(':').Select(str => int.Parse(str)).ToArray() : new int[0];
            int[] speeds = lines[2].Length > 0 ? lines[2].Split(':').Select(str => int.Parse(str)).ToArray() : new int[0];
            int[] speedsStarts = lines[3].Length > 0 ? lines[3].Split(':').Select(str => int.Parse(str)).ToArray() : new int[0];
            for(int i = 0; i < speedsStarts.Length; i++) {
                this.speeds.Add(new LevelSpeed(speeds[i], speedsStarts[i]));
                objects.Add(new LevelObject(LevelObject.speedChar, speedsStarts[i], this.speeds));
            }
            for(int i = 0; i < objectsSteps.Length; i++) {
                int step = objectsSteps[i];
                objects.Add(new LevelObject(lines[0][i], step, this.speeds, objects));
            }
            this.speeds.Sort((speed1, speed2) => speed1.step.CompareTo(speed2.step));
            string[] meta = lines[4].Split(':');
            metadata = new LevelMetadata(this, meta, name);
        }
    }
    public class LevelSpeed {
        public int speed;
        public int step;
        public LevelSpeed(int speed, int step) {
            this.speed = speed;
            this.step = step;
        }
    }
    public class LevelObject {
        static readonly string[] lines = new string[] {
            "1234567890-=",
            "qwertyuiop[]",
            "asdfghjkl;'",
            "zxcvbnm,./"
        };
        public const char speedChar = '>';
        public const char holdChar = '│';
        public const int hitRange = 1;
        public const int missRange = 2;
        public Vector2 position;
        public Vector2 startPosition;
        public char character;
        public Keyboard.Key key;
        public readonly int step;
        public readonly int directionLayer;
        Color hitColor;
        static readonly Color color = ColorScheme.white;
        static readonly Color nextDirLayerColor = ColorScheme.lightDarkGray;
        static readonly Color speedColor = ColorScheme.blue;

        public bool removed;
        float removeAnimationTime;

        public bool ignore = false;

        public LevelObject(char character, int step, List<LevelSpeed> speeds, List<LevelObject> objects = null) {
            int x = 0;
            int xLineOffset = 0;
            int mul = 90 / lines.Select(line => line.Length).Max();
            foreach(string line in lines) {
                if(line.Contains(character)) {
                    x = (line.IndexOf(character) + 1) * (mul - 1) + xLineOffset * mul / 3;
                    break;
                }
                xLineOffset++;
            }
            if(character == holdChar && objects != null) {
                List<LevelObject> existingObjects = new List<LevelObject>(objects);
                existingObjects.Sort((obj1, obj2) => -obj1.step.CompareTo(obj2.step));
                foreach(LevelObject obj in existingObjects) {
                    if(obj.step <= step && obj.character != speedChar && obj.character != holdChar) {
                        x = obj.position.x;
                        key = obj.key;
                        obj.ignore = true;
                        break;
                    }
                }
            }
            List<LevelSpeed> existingSpeeds = new List<LevelSpeed>(speeds);
            existingSpeeds.Sort((spd1, spd2) => spd1.step.CompareTo(spd2.step));
            startPosition = new Vector2(x, Map.linePos.y - (int)Game.StepsToOffset(step, existingSpeeds));
            position = new Vector2(startPosition);
            this.character = character;
            this.step = step;
            directionLayer = Game.StepsToDirectionLayer(step, existingSpeeds);
            switch(char.ToUpper(character)) {
                case '1': key = Keyboard.Key.Num1; break;
                case '2': key = Keyboard.Key.Num2; break;
                case '3': key = Keyboard.Key.Num3; break;
                case '4': key = Keyboard.Key.Num4; break;
                case '5': key = Keyboard.Key.Num5; break;
                case '6': key = Keyboard.Key.Num6; break;
                case '7': key = Keyboard.Key.Num7; break;
                case '8': key = Keyboard.Key.Num8; break;
                case '9': key = Keyboard.Key.Num9; break;
                case '0': key = Keyboard.Key.Num0; break;
                case '-': key = Keyboard.Key.Hyphen; break;
                case '=': key = Keyboard.Key.Equal; break;
                case 'Q': key = Keyboard.Key.Q; break;
                case 'W': key = Keyboard.Key.W; break;
                case 'E': key = Keyboard.Key.E; break;
                case 'R': key = Keyboard.Key.R; break;
                case 'T': key = Keyboard.Key.T; break;
                case 'Y': key = Keyboard.Key.Y; break;
                case 'U': key = Keyboard.Key.U; break;
                case 'I': key = Keyboard.Key.I; break;
                case 'O': key = Keyboard.Key.O; break;
                case 'P': key = Keyboard.Key.P; break;
                case '[': key = Keyboard.Key.LBracket; break;
                case ']': key = Keyboard.Key.RBracket; break;
                case 'A': key = Keyboard.Key.A; break;
                case 'S': key = Keyboard.Key.S; break;
                case 'D': key = Keyboard.Key.D; break;
                case 'F': key = Keyboard.Key.F; break;
                case 'G': key = Keyboard.Key.G; break;
                case 'H': key = Keyboard.Key.H; break;
                case 'J': key = Keyboard.Key.J; break;
                case 'K': key = Keyboard.Key.K; break;
                case 'L': key = Keyboard.Key.L; break;
                case ';': key = Keyboard.Key.Semicolon; break;
                case '\'': key = Keyboard.Key.Quote; break;
                case 'Z': key = Keyboard.Key.Z; break;
                case 'X': key = Keyboard.Key.X; break;
                case 'C': key = Keyboard.Key.C; break;
                case 'V': key = Keyboard.Key.V; break;
                case 'B': key = Keyboard.Key.B; break;
                case 'N': key = Keyboard.Key.N; break;
                case 'M': key = Keyboard.Key.M; break;
                case ',': key = Keyboard.Key.Comma; break;
                case '.': key = Keyboard.Key.Period; break;
                case '/': key = Keyboard.Key.Slash; break;
            }
        }

        static Color HitColor(int step, bool hold) {
            int distance = Math.Abs(step - Game.roundedSteps);
            bool hit = distance >= hitRange;
            bool miss = distance >= missRange || (hit && hold);
            return miss ? ColorScheme.red : hit ? ColorScheme.yellow : ColorScheme.green;
        }
        Color NormalColor() {
            return NormalColor(directionLayer, Game.currentDirectionLayer);
        }
        static Color NormalColor(int noteDirLayer, int curDirLayer) {
            int difference = Math.Abs(noteDirLayer - curDirLayer);
            return difference == 0 ? color : difference == 1 ? nextDirLayerColor : Color.Transparent;
        }
        public void Draw() {
            if(removed && !ignore) {
                if(removeAnimationTime <= 0f) {
                    List<LevelObject> samePosObjects = Map.currentLevel.objects.FindAll(obj => obj.position == position && obj.ignore && obj != this);
                    if(samePosObjects.Count > 0) {
                        samePosObjects.ForEach(obj => Map.currentLevel.objects.Remove(obj));
                    }
                }
                Color startColor = hitColor;
                Renderer.instance.SetCellColor(position, Renderer.AnimateColor(removeAnimationTime, startColor, ColorScheme.white, 3f),
                                                                                                                     Renderer.AnimateColor(removeAnimationTime, startColor, Color.Transparent, 3f));
                if(removeAnimationTime >= 1f) _ = Map.currentLevel.objects.Remove(this);
                removeAnimationTime += Core.deltaTime;
                return;
            }
            if(!ignore && (!Game.editing || !Game.StepPassedLine(step, 1)))
                Renderer.instance.SetCharacter(position, character, character == speedChar ? speedColor : NormalColor(), Color.Transparent);
            if(!Game.editing && Game.StepPassedLine(step)) {
                if(character == speedChar || ignore) {
                    _ = Map.currentLevel.objects.Remove(this);
                }
                else if(Game.StepPassedLine(step, character == holdChar ? 1 : missRange)) {
                    Miss();
                    Game.RecalculateAccuracy();
                    removed = true;
                    hitColor = HitColor(step, character == holdChar);
                }
                else if(Game.auto || character == holdChar) {
                    CheckPress();
                }
            }
        }
        public void CheckHit() {
            if(character == holdChar ? step == Game.roundedSteps : Game.StepPassedLine(step, -hitRange)) {
                Hit();
            }
            else {
                Miss();
            }
            PlayHitsound();
            Game.RecalculateAccuracy();
            removed = true;
            hitColor = HitColor(step, character == holdChar);
        }
        public void CheckPress() {
            if(removed || ignore) return;
            if(Game.auto || Keyboard.IsKeyPressed(key)) {
                CheckHit();
            }
        }
        void PlayHitsound() {
            if(character == speedChar || ignore || removed) return;
            if(character == holdChar) Game.tickSound.Play();
            else Game.hitSound.Play();
        }
        void Hit() {
            Game.health += Map.currentLevel.metadata.hpRestorage;
            int score = step == (int)Game.steps || character == holdChar ? 10 : 5;
            Game.combo++;
            Game.maxCombo = Math.Max(Game.combo, Game.maxCombo);
            Game.score += score * Game.combo;
            Game.scores[score / 5]++;
        }
        void Miss() {
            Game.health -= Map.currentLevel.metadata.hpDrain;
            Game.combo = 0;
            Game.scores[0]++;
        }
        public void Step() {
            if(removed) return;
            position.y = startPosition.y + Game.roundedOffset;
            if(Game.editing && Game.music.Status == SoundStatus.Playing && step == (int)Game.steps)
                PlayHitsound();
        }


        public override bool Equals(object obj) {
            return obj is LevelObject @object &&
                   EqualityComparer<Vector2>.Default.Equals(position, @object.position) &&
                   character == @object.character &&
                   step == @object.step;
        }
        public override int GetHashCode() {
            return HashCode.Combine(position, character, step);
        }
        public static bool operator ==(LevelObject left, LevelObject right) {
            return left is null ? right is null : left.Equals(right);
        }
        public static bool operator !=(LevelObject left, LevelObject right) {
            return !(left == right);
        }
    }
}
