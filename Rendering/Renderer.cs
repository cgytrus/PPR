using System;
using System.Collections.Generic;
using System.IO;

using PPR.GUI;
using PPR.Main;
using PPR.Main.Levels;
using PPR.Properties;

using SFML.Graphics;
using SFML.System;
using SFML.Window;

namespace PPR.Rendering {
    public class Renderer {
        public static Renderer instance;

        readonly Dictionary<Vector2, float> randomColorAnimationOffsets;

        public readonly Dictionary<Vector2, Color> backgroundColors;
        public readonly Dictionary<Vector2, Color> foregroundColors;
        public readonly Dictionary<Vector2, char> displayString;
        public Vector2 fontSize;
        public readonly int width;
        public int windowWidth;
        public readonly int height;
        public int windowHeight;
        public int frameRate;
        public BitmapText text;
        public RenderWindow window;
        readonly Shader bloom = Shader.FromString(File.ReadAllText(Path.Combine("resources", "bloom_vert.glsl")), null,
                                                                                                                     File.ReadAllText(Path.Combine("resources", "bloom_frag.glsl")));
        public RenderTexture bloomRT;
        public RenderTexture finalRT;

        public Vector2 mousePosition = new Vector2(-1, -1);
        public bool leftButtonPressed = false;

        public Renderer(int width, int height, int frameRate) {
            instance = this;

            string[] fontMappingsLines = File.ReadAllLines(Path.Combine("resources", "fonts", Settings.Default.font, "mappings.txt"));
            string[] fontSizeStr = fontMappingsLines[0].Split(',');
            fontSize = new Vector2(int.Parse(fontSizeStr[0]), int.Parse(fontSizeStr[1]));

            this.width = width;
            windowWidth = width * fontSize.x;
            this.height = height;
            windowHeight = height * fontSize.y;
            this.frameRate = frameRate;

            randomColorAnimationOffsets = new Dictionary<Vector2, float>(this.width * this.height);

            backgroundColors = new Dictionary<Vector2, Color>(this.width * this.height);
            foregroundColors = new Dictionary<Vector2, Color>(this.width * this.height);
            displayString = new Dictionary<Vector2, char>(this.width * this.height);

            window = new RenderWindow(new VideoMode((uint)windowWidth, (uint)windowHeight), "Press Press Revolution", Styles.Close);
            bloomRT = new RenderTexture((uint)windowWidth, (uint)windowHeight);
            finalRT = new RenderTexture((uint)windowWidth, (uint)windowHeight);

            SubscribeWindowEvents();

            if(frameRate < 0) window.SetVerticalSyncEnabled(true);
            else if(frameRate != 0) window.SetFramerateLimit((uint)frameRate);

            BitmapFont font = new BitmapFont(new Image(Path.Combine("resources", "fonts", Settings.Default.font, "font.png")), fontMappingsLines[1], fontSize);
            text = new BitmapText(font, new Vector2(width, height)) {
                backgroundColors = backgroundColors,
                foregroundColors = foregroundColors,
                text = displayString
            };
        }
        public void SubscribeWindowEvents() {
            window.KeyPressed += Core.game.KeyPressed;
            window.MouseWheelScrolled += Core.game.MouseWheelScrolled;
            window.LostFocus += Core.game.LostFocus;
            window.GainedFocus += Core.game.GainedFocus;
            window.Closed += (_, __) => Core.game.End();
            window.MouseMoved += UpdateMousePosition;
            window.MouseButtonPressed += (_, e) => {
                if(e.Button == Mouse.Button.Left) leftButtonPressed = true;
            };
            window.MouseButtonReleased += (_, e) => {
                if(e.Button == Mouse.Button.Left) leftButtonPressed = false;
            };
            window.SetKeyRepeatEnabled(false);
        }
        public void SetFullscreen(bool fullscreen = false) {
            window.Close();
            if(!fullscreen) {
                Core.renderer.windowWidth = Core.renderer.width * Core.renderer.fontSize.x;
                Core.renderer.windowHeight = Core.renderer.height * Core.renderer.fontSize.y;
            }
            window = fullscreen
                ? new RenderWindow(VideoMode.FullscreenModes[0], "Press Press Revolution", Styles.Fullscreen)
                : new RenderWindow(new VideoMode((uint)windowWidth, (uint)windowHeight), "Press Press Revolution", Styles.Close);
            SubscribeWindowEvents();
            UpdateWindow();
        }
        public void UpdateWindow() {
            FloatRect visibleArea = new FloatRect(0, 0, windowWidth, windowHeight);
            if(Settings.Default.fullscreen) {
                VideoMode videoMode = VideoMode.FullscreenModes[0];

                Vector2 oldCenter = new Vector2(windowWidth / 2, windowHeight / 2);

                windowWidth = (int)videoMode.Width;
                windowHeight = (int)videoMode.Height;

                Vector2 newCenter = new Vector2(windowWidth / 2, windowHeight / 2);

                visibleArea = new FloatRect(oldCenter.x - newCenter.x, oldCenter.y - newCenter.y, windowWidth, windowHeight);
            }
            else {
                window.Size = new Vector2u((uint)windowWidth, (uint)windowHeight);

                if(frameRate < 0) window.SetVerticalSyncEnabled(true);
                else if(frameRate != 0) window.SetFramerateLimit((uint)frameRate);
            }
            window.SetView(new View(visibleArea));
            bloomRT = new RenderTexture((uint)windowWidth, (uint)windowHeight);
            finalRT = new RenderTexture((uint)windowWidth, (uint)windowHeight);
        }

        public void ClearText() {
            backgroundColors.Clear();
            foregroundColors.Clear();
            displayString.Clear();
        }
        void UpdateMousePosition(object caller, MouseMoveEventArgs mouse) {
            if(!window.HasFocus()) {
                mousePosition = new Vector2(-1, -1);
                return;
            }
            View windowView = window.GetView();
            mousePosition = new Vector2((int)((mouse.X + windowView.Center.X - windowView.Size.X / 2) / fontSize.x),
                (int)((mouse.Y + windowView.Center.Y - windowView.Size.Y / 2) / fontSize.y));
        }
        public void Update() {
            ClearText();
            Map.Draw();
            UI.Draw();
            RenderImage();
        }
        public void RenderImage() {
            text.backgroundColors = backgroundColors;
            text.foregroundColors = foregroundColors;
            text.text = displayString;

            Sprite defSprite = new Sprite(text.renderTexture.Texture);

            bloomRT.Clear();
            finalRT.Clear();

            if(Settings.Default.bloom) {
                Shader.Bind(bloom);

                bloom.SetUniform("image", defSprite.Texture);
                bloom.SetUniform("horizontal", false);
                bloomRT.Draw(new Sprite(defSprite), new RenderStates(bloom));
                bloomRT.Display();

                bloom.SetUniform("image", bloomRT.Texture);
                bloom.SetUniform("horizontal", true);
                finalRT.Draw(new Sprite(bloomRT.Texture), new RenderStates(bloom));

                Shader.Bind(null);
            }

            finalRT.Draw(defSprite, new RenderStates(BlendMode.Add));

            finalRT.Display();
        }
        public void Draw() {
            window.Clear();
            window.Draw(new Sprite(finalRT.Texture));
        }
        public enum TextAlignment { Left, Center, Right }
        public void DrawText(Vector2 position, string text, Color foregroundColor, Color backgroundColor, TextAlignment align = TextAlignment.Left, bool replacingSpaces = true) {
            if(text.Length == 0) return; // Don't do anything, if the text is empty
            if(text.Length == 1) {
                SetCharacter(position, text[0], foregroundColor, backgroundColor);
            }

            int posX = position.x - align switch
            {
                TextAlignment.Right => text.Length - 1,
                TextAlignment.Center => (int)MathF.Ceiling(text.Length / 2f),
                _ => 0
            };

            string[] lines = text.Split('\n');
            int height = lines.Length;

            if(height == 1) DrawOneLineText(new Vector2(posX, position.y), text, foregroundColor, backgroundColor, replacingSpaces);

            for(int l = 0; l < height; l++) {
                string curLine = lines[l];
                DrawOneLineText(new Vector2(posX, position.y + l), curLine, foregroundColor, backgroundColor, replacingSpaces);
            }
        }
        void DrawOneLineText(Vector2 position, string text, Color foregroundColor, Color backgroundColor, bool replacingSpaces = true) {
            for(int x = 0; x < text.Length; x++) {
                char curChar = text[x];
                if(!replacingSpaces && curChar == ' ') continue;
                SetCharacter(new Vector2(position.x + x, position.y), curChar, foregroundColor, backgroundColor);
            }
        }

        public void SetCharacter(Vector2 position, char character) {
            if(position.InBounds(0, 0, width - 1, height - 1)) {
                // If a character is a space or null, remove it from a dictionary
                if(character == ' ' || character == '\0') _ = displayString.Remove(position);
                else displayString[position] = character;
            }
        }
        public void SetCharacter(Vector2 position, char character, Color foregroundColor, Color backgroundColor) {
            SetCharacter(position, foregroundColor == backgroundColor || foregroundColor.A == 0 ? ' ' : character);
            SetCellColor(position, foregroundColor, backgroundColor);
        }
        public char GetCharacter(Vector2 position) {
            return !displayString.ContainsKey(position) ? '\0' : displayString[position];
        }
        public void SetCellColor(Vector2 position, Color foregroundColor, Color backgroundColor) {
            if(position.InBounds(0, 0, width - 1, height - 1)) {
                if(backgroundColor == Color.Black) _ = backgroundColors.Remove(position);
                else if(backgroundColor.A != 0) backgroundColors[position] = backgroundColor;

                if(foregroundColor == Color.White) _ = foregroundColors.Remove(position);
                else foregroundColors[position] = foregroundColor;
            }
        }
        public static Color LerpColors(Color a, Color b, float t) {
            return t <= 0f ? a : t >= 1f ? b :
                new Color((byte)MathF.Floor(a.R + (b.R - a.R) * t), (byte)MathF.Floor(a.G + (b.G - a.G) * t), (byte)MathF.Floor(a.B + (b.B - a.B) * t), (byte)MathF.Floor(a.A + (b.A - a.A) * t));
        }
        public static Color AnimateColor(float time, Color start, Color end, float rate) {
            float t = Math.Clamp(time * rate, 0f, 1f);
            return LerpColors(start, end, t);
        }
        public void UpdateRandomColorAnimationOffset(Vector2 position, float min, float max) {
            if(position.InBounds(0, 0, width - 1, height - 1)) {
                randomColorAnimationOffsets[position] = new Random().NextFloat(min, max);
            }
        }
        public float GetRandomColorAnimationOffset(Vector2 position) {
            return !randomColorAnimationOffsets.ContainsKey(position) ? 0f : randomColorAnimationOffsets[position];
        }
        public Color GetCellBackgroundColor(Vector2 position) {
            return !backgroundColors.ContainsKey(position) ? Color.Black : backgroundColors[position];
        }
        public Color GetCellForegroundColor(Vector2 position) {
            return !foregroundColors.ContainsKey(position) ? Color.White : foregroundColors[position];
        }
    }
}