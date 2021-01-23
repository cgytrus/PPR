﻿using System;

using PPR.Main;

using PRR;

using SFML.Graphics;
using SFML.System;

using Alignment = PRR.Renderer.Alignment;

namespace PPR.GUI.Elements {
    public class Text : UIElement {
        public override string type => "text";

        public string text {
            get => _text;
            set {
                _text = value;
                lines = value.Replace("\n\r", "\n").Replace("\r\n", "\n").Replace("\r", "\n").Split("\n");
            }
        }

        public string[] lines { get; private set; }
        public Alignment align { get; set; }
        public bool replacingSpaces { get; set; }
        public bool invertOnDarkBackground { get; set; }
        private Color foregroundColor => ColorScheme.GetColor($"text_{id}_fg");
        private Color backgroundColor => ColorScheme.GetColor($"text_{id}_bg");
        private string _text;

        public Text(string uid, string id, Vector2i? position, Vector2f? anchor, UIElement parent, string text,
            Alignment align, bool replacingSpaces, bool invertOnDarkBackground) : base(uid, id, position,
            new Vector2i(), anchor, parent) {
            this.text = text;
            this.align = align;
            this.replacingSpaces = replacingSpaces;
            this.invertOnDarkBackground = invertOnDarkBackground;
        }

        public override void Draw(Func<Vector2i, RenderCharacter, (Vector2i, RenderCharacter)> transition) =>
            Core.renderer.DrawLines(globalPosition, lines, foregroundColor, backgroundColor, align, replacingSpaces,
                invertOnDarkBackground, transition);
    }
}
