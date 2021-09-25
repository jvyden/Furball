using MonoGame.Extended;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Furball.Engine.Engine.Graphics.Drawables.Managers;

namespace Furball.Engine.Engine.Graphics.Drawables.UiElements {
    public class UiProgressBarDrawable : ManagedDrawable {
        private string _formatString = "{0:0}%";

        public TextDrawable TextDrawable;

        public  float   Progress;
        public  Color   OutlineColor;
        public  Vector2 BarSize;
        private float   _progressWidth;
        public  float   OutlineThickness = 1f;

        public override Vector2 Size => this.BarSize;

        public UiProgressBarDrawable(byte[] font, Vector2 size, Color outlineColor, Color barColor, Color textColor) {
            this.BarSize                    = size;
            this.OutlineColor               = outlineColor;
            this.ColorOverride              = barColor;

            this.TextDrawable = new(font, "", size.Y * 0.9f) {
                ColorOverride = textColor
            };
        }

        public override void Update(GameTime time) {
            this.TextDrawable.Text       = string.Format(this._formatString, this.Progress * 100);

            this._progressWidth = this.BarSize.X * this.Progress;

            base.Update(time);
        }

        public override void Draw(GameTime time, SpriteBatch batch, DrawableManagerArgs args) {
            batch.FillRectangle(args.Position, new Vector2(this._progressWidth, this.BarSize.Y), args.Color, args.LayerDepth);
            batch.DrawRectangle(args.Position, this.BarSize, this.OutlineColor, this.OutlineThickness, args.LayerDepth);
            
            // FIXME: this is a bit of a hack, it should definitely be done differently
            DrawableManagerArgs tempArgs = args;
            tempArgs.Position.X += this.BarSize.X / 2f * args.Scale.X;
            tempArgs.Position.Y += this.BarSize.Y / 2f * args.Scale.X;
            tempArgs.Color      =  this.TextDrawable.ColorOverride;
            tempArgs.Origin     =  new Vector2(this.TextDrawable.Size.X / 2f, this.TextDrawable.Size.Y / 2) * args.Scale;
            tempArgs.Scale      /= FurballGame.VerticalRatio;
            this.TextDrawable.Draw(time, batch, tempArgs);
        }
    }
}