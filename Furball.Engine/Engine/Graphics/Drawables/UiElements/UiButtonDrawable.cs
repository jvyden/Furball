using Furball.Engine.Engine.Graphics.Drawables.Managers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;

namespace Furball.Engine.Engine.Graphics.Drawables.UiElements {
    public class UiButtonDrawable : ManagedDrawable {
        private string _text;
        public string Text => this._text;
        public TextDrawable TextDrawable;

        private float _margin;
        public float Margin => this._margin;

        public Color OutlineColor = Color.Black;

        public UiButtonDrawable(string text, byte[] font, float size, Color textColor, Color outlineColor, float margin = 5f) {
            this.TextDrawable = new TextDrawable(font, text, size);

            this.OnMove += this.RecalculateSize;

            this._margin = margin;
            this._text   = text;

            this.Size = new Vector2();

            this.RecalculateSize(this, this.Position);

            this.TextDrawable.ColorOverride = textColor;
            this.OutlineColor               = outlineColor;
        }

        private void RecalculateSize(object sender, Vector2 newPos) {
            (float textDrawableSizeX, float textDrawableSizeY) = this.TextDrawable.Size;

            this.Size = new Vector2(textDrawableSizeX + this._margin * 2f, textDrawableSizeY + this._margin * 2f);
        }

        public override void Draw(GameTime time, SpriteBatch batch, DrawableManagerArgs args) {
            batch.FillRectangle(args.Position, this.Size, args.Color);
            batch.DrawRectangle(args.Position, this.Size, this.OutlineColor, 1f, args.LayerDepth);

            // FIXME: this is a bit of a hack, it should definitely be done differently
            DrawableManagerArgs tempArgs = args;
            tempArgs.Position.X += this._margin * args.Scale.X;
            tempArgs.Position.Y += this._margin * args.Scale.Y;
            tempArgs.Color      =  this.TextDrawable.ColorOverride;
            tempArgs.Scale      /= FurballGame.VerticalRatio;
            this.TextDrawable.Draw(time, batch, tempArgs);
        }
    }
}
