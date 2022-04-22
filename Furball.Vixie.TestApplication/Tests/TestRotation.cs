using System.Numerics;
using Furball.Vixie.Backends.Shared;
using Furball.Vixie.Backends.Shared.Renderers;
using Furball.Vixie.Helpers.Helpers;
using ImGuiNET;


namespace Furball.Vixie.TestApplication.Tests {
    public class TestRotation : GameComponent {
        private IQuadRenderer _quadRendererGl;
        private Texture       _whiteTextureGl;

        public override void Initialize() {
            this._quadRendererGl = GraphicsBackend.Current.CreateTextureRenderer();
            this._whiteTextureGl = Resources.CreateTexture(ResourceHelpers.GetByteResource("Resources/pippidonclear0.png"));

            base.Initialize();
        }

        private float _rotation = 1f;

        public override void Draw(double deltaTime) {
            GraphicsBackend.Current.Clear();

            this._quadRendererGl.Begin();

            for(int i = 0; i != 360; i ++)
                this._quadRendererGl.Draw(this._whiteTextureGl, new Vector2(1280 / 2, 720 / 2), Vector2.One, (float) i * (3.1415f / 180f) + this._rotation);

            this._quadRendererGl.End();


            #region ImGui menu

            ImGui.DragFloat("Rotation", ref this._rotation, 0.01f, 0f, 8f);

            if (ImGui.Button("Go back to test selector")) {
                this.BaseGame.Components.Add(new BaseTestSelector());
                this.BaseGame.Components.Remove(this);
            }

            #endregion

            base.Draw(deltaTime);
        }

        public override void Dispose() {
            this._quadRendererGl.Dispose();
            this._whiteTextureGl.Dispose();

            base.Dispose();
        }
    }
}
