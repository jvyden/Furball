using System.Drawing;
using System.Numerics;
using FontStashSharp.Interfaces;
using Furball.Vixie.Graphics;
using Furball.Vixie.Graphics.Renderers;
using Color=System.Drawing.Color;

namespace Furball.Vixie.FontStashSharp {
    public class VixieFontStashRenderer : IFontStashRenderer {
        private ITextureRenderer _renderer;

        public VixieFontStashRenderer(ITextureRenderer renderer) {
            this._renderer      = renderer;
            this.TextureManager = new VixieTexture2dManager();
        }

        public void Draw(object texture, Vector2 pos, Rectangle? src, Color color, float rotation, Vector2 origin, Vector2 scale, float depth) {
            this._renderer.Draw(texture as Texture, pos - (origin * scale), Vector2.Zero, scale, rotation, new Graphics.Color(color.R, color.G, color.B, color.A), src, TextureFlip.FlipVertical);
        }

        public ITexture2DManager TextureManager { get; }
    }
}