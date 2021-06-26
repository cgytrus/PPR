using System;
using System.Collections.Generic;
using System.IO;

using PER.Abstractions.Renderer;
using PER.Util;

using SFML.Graphics;
using SFML.System;
using SFML.Window;

using Color = PER.Util.Color;

namespace PRR {
    public class Renderer : IRenderer {
        public string title { get; private set; }
        public int width { get; private set; }
        public int height { get; private set; }
        
        public int framerate {
            get => _framerate;
            set {
                _framerate = value;
                if(_window == null) return;
                UpdateFramerate();
            }
        }

        public bool fullscreen {
            get => _fullscreen;
            set {
                _fullscreen = value;
                Reset();
            }
        }

        public string font {
            get => _font;
            set {
                _font = value;
                Reset();
            }
        }

        public Vector2Int fontSize { get; private set; }
        public string icon { get; set; }

        public bool open => _window.IsOpen;
        public bool focused => _window.HasFocus();
        
        public Color clear { get; set; } = Color.black;
        
        public Vector2Int mousePosition { get; private set; } = new(-1, -1);
        public Vector2 accurateMousePosition { get; private set; } = new(-1f, -1f);

        private Dictionary<Vector2Int, RenderCharacter> _display;

        private int _framerate;
        private bool _fullscreen;
        private string _font;
        
        private readonly Shader _bloomFirstPass = Shader.FromString(
            File.ReadAllText(Path.Join("resources", "bloom_vert.glsl")), null,
            File.ReadAllText(Path.Join("resources", "bloom_frag.glsl")));

        private readonly Shader _bloomSecondPass = Shader.FromString(
            File.ReadAllText(Path.Join("resources", "bloom_vert.glsl")), null,
            File.ReadAllText(Path.Join("resources", "bloom_frag.glsl")));

        private readonly Shader _bloomBlend = Shader.FromString(
            File.ReadAllText(Path.Join("resources", "bloom_vert.glsl")), null,
            File.ReadAllText(Path.Join("resources", "bloom-blend_frag.glsl")));
        
        private RenderTexture _bloomRT1;
        private RenderTexture _bloomRT2;

        private Text _text;
        private Vector2f _textPosition;
        private RenderWindow _window;

        public void Setup(RendererSettings settings) {
            title = settings.title;
            width = settings.width;
            height = settings.height;
            _framerate = settings.framerate;
            _fullscreen = settings.fullscreen;
            _font = settings.font;
            icon = settings.icon;
            
            CreateWindow();

            _bloomFirstPass.SetUniform("horizontal", true);
            _bloomSecondPass.SetUniform("horizontal", false);
        }
        
        public void Loop() => _window.DispatchEvents();

        public void Stop() => _window?.Close();

        public void Reset(RendererSettings settings) {
            Stop();
            Setup(settings);
        }

        public void Reset() => Reset(new RendererSettings(this));

        private void CreateWindow() {
            if(_window?.IsOpen ?? false) _window.Close();
            UpdateFont();
            
            VideoMode videoMode = fullscreen ? VideoMode.FullscreenModes[0] :
                new VideoMode((uint)(width * fontSize.x), (uint)(height * fontSize.y));

            _window = new RenderWindow(videoMode, title, fullscreen ? Styles.Fullscreen : Styles.Close);
            _window.SetView(new View(new Vector2f(videoMode.Width / 2f, videoMode.Height / 2f),
                new Vector2f(videoMode.Width, videoMode.Height)));
            
            if(File.Exists(this.icon)) {
                Image icon = new(this.icon);
                _window.SetIcon(icon.Size.X, icon.Size.Y, icon.Pixels);
            }
            
            _window.Closed += (_, _) => Stop();
            _window.MouseMoved += UpdateMousePosition;
            _window.SetKeyRepeatEnabled(false);
                
            _bloomRT1 = new RenderTexture(videoMode.Width, videoMode.Height);
            _bloomRT2 = new RenderTexture(videoMode.Width, videoMode.Height);
                
            _textPosition = new Vector2f((videoMode.Width - _text.imageWidth) / 2f,
                (videoMode.Height - _text.imageHeight) / 2f);

            UpdateFramerate();
        }

        private void UpdateFramerate() {
            _window.SetFramerateLimit(_framerate <= 0 ? 0 : (uint)_framerate);
            _window.SetVerticalSyncEnabled(_framerate == (int)ReservedFramerates.Vsync);
        }

        private void UpdateFont() {
            string[] fontMappingsLines = File.ReadAllLines(Path.Join(this.font, "mappings.txt"));
            string[] fontSizeStr = fontMappingsLines[0].Split(',');
            fontSize = new Vector2Int(int.Parse(fontSizeStr[0]), int.Parse(fontSizeStr[1]));
                
            _display = new Dictionary<Vector2Int, RenderCharacter>(width * height);

            Font font = new(new Image(Path.Join(this.font, "font.png")), fontMappingsLines[1], fontSize);
            _text = new Text(font, new Vector2Int(width, height)) { text = _display };
        }

        private void UpdateMousePosition(object caller, MouseMoveEventArgs mouse) {
            if(!_window.HasFocus()) {
                mousePosition = new Vector2Int(-1, -1);
                accurateMousePosition = new Vector2(-1f, -1f);
                return;
            }

            accurateMousePosition = new Vector2((mouse.X - _window.Size.X / 2f + _text.imageWidth / 2f) / fontSize.x,
                (mouse.Y - _window.Size.Y / 2f + _text.imageHeight / 2f) / fontSize.y);
            mousePosition = new Vector2Int((int)accurateMousePosition.x, (int)accurateMousePosition.y);
        }

        public void Clear() => _display.Clear();

        public void Draw() => Draw(true);

        public void Draw(bool bloom) {
            SFML.Graphics.Color background = SfmlConverters.ToSfmlColor(clear);
            
            _text.RebuildQuads(_textPosition);

            if(bloom) {
                _bloomRT1.Clear(background);
                _text.DrawQuads(_bloomRT1);

                _bloomFirstPass.SetUniform("image", _bloomRT1.Texture);
                _bloomRT2.Clear(background);
                _bloomRT2.Draw(new Sprite(_bloomRT1.Texture), new RenderStates(_bloomFirstPass));

                _bloomSecondPass.SetUniform("image", _bloomRT2.Texture);
                _bloomRT1.Clear(background);
                _bloomRT1.Draw(new Sprite(_bloomRT2.Texture), new RenderStates(_bloomSecondPass));

                _bloomRT2.Clear(background);
                _text.DrawQuads(_bloomRT2);

                _bloomRT1.Display();
                _bloomRT2.Display();

                _bloomBlend.SetUniform("imageA", _bloomRT2.Texture);
                _bloomBlend.SetUniform("imageB", _bloomRT1.Texture);
                _window.Draw(new Sprite(_bloomRT1.Texture), new RenderStates(_bloomBlend));
            }
            else {
                _window.Clear(background);
                _text.DrawQuads(_window);
            }
            
            _window.Display();
        }

        public void DrawText(Vector2Int position, string text, Color foregroundColor, Color backgroundColor,
            HorizontalAlignment align = HorizontalAlignment.Left, RenderFlags flags = RenderFlags.Default) {
            switch(text.Length) {
                case 0: return;
                case 1: {
                    DrawCharacter(position, new RenderCharacter(text[0], backgroundColor, foregroundColor), flags);
                    return;
                }
            }

            int posX = position.x - align switch {
                HorizontalAlignment.Right => text.Length - 1,
                HorizontalAlignment.Middle => (int)MathF.Floor(text.Length / 2f),
                _ => 0
            };

            int x = 0;
            foreach(char curChar in text) {
                Vector2Int charPos = new(posX + x, position.y);
                DrawCharacter(charPos, new RenderCharacter(curChar, backgroundColor, foregroundColor), flags);
                x++;
            }
        }

        public void DrawText(Vector2Int position, string[] lines, Color foregroundColor, Color backgroundColor,
            HorizontalAlignment align = HorizontalAlignment.Left, RenderFlags flags = RenderFlags.Default) {
            for(int i = 0; i < lines.Length; i++)
                DrawText(position + new Vector2Int(0, i), lines[i], foregroundColor, backgroundColor,
                    align, flags);
        }

        public void DrawCharacter(Vector2Int position, RenderCharacter character,
            RenderFlags flags = RenderFlags.Default) {
            if(position.x < 0 || position.y < 0 || position.x >= width || position.y >= height) return;
            
            if(flags.HasFlag(RenderFlags.BackgroundAlphaBlending)) {
                RenderCharacter currentCharacter = GetCharacter(position);
                Color background = Color.Blend(currentCharacter.background, character.background);
                //Color foreground = Color.Blend(background, character.foreground);
                character = new RenderCharacter(background, character.foreground, character);
            }

            if(flags.HasFlag(RenderFlags.InvertedBackgroundAsForegroundColor)) {
                RenderCharacter currentCharacter = GetCharacter(position);
                character = new RenderCharacter(character.character, character.background,
                    Color.white - currentCharacter.background);
            }
            
            if(IsRenderCharacterEmpty(character)) _display.Remove(position);
            else _display[position] = character;
        }

        public RenderCharacter GetCharacter(Vector2Int position) => _display.ContainsKey(position) ? _display[position] :
            new RenderCharacter('\0', Color.transparent, Color.transparent);

        private bool IsRenderCharacterEmpty(RenderCharacter renderCharacter) =>
            renderCharacter.background.a == 0 &&
            (!CharacterExists(renderCharacter.character) || renderCharacter.foreground.a == 0);

        private bool CharacterExists(char character) => _text.font.characters.ContainsKey(character);
    }
}
