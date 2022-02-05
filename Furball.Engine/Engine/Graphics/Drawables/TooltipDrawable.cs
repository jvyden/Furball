
using Furball.Engine.Engine.Graphics.Drawables.Primitives;
using Furball.Vixie.Graphics;


namespace Furball.Engine.Engine.Graphics.Drawables {
    public class TooltipDrawable : CompositeDrawable {
        private readonly TextDrawable               _textDrawable;
        private readonly RectanglePrimitiveDrawable _backgroundRect;

        public TooltipDrawable() {
            this._backgroundRect = new(new(0), new(100, 20), 2, true) {
                ColorOverride = new(0, 0, 0, 155)
            };
            this._textDrawable = new(new(0), FurballGame.DEFAULT_FONT, "", 20) {
                ColorOverride = Color.White
            };

            this._drawables.Add(this._backgroundRect);
            this._drawables.Add(this._textDrawable);

            this.Clickable   = false;
            this.CoverClicks = false;
            this.Hoverable   = false;
            this.CoverHovers = false;
        }

        /// <summary>
        ///     Sets the tooltip to the specified text
        /// </summary>
        /// <param name="text"></param>
        public void SetTooltip(string text) {
            this._textDrawable.Text       = text;
            this._backgroundRect.RectSize = this._textDrawable.Size;
        }
    }
}