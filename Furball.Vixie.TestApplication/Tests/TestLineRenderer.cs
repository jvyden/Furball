using System;
using System.Drawing;
using System.Globalization;
using System.Numerics;
using Furball.Vixie.Graphics;
using Furball.Vixie.ImGuiHelpers;
using ImGuiNET;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;

namespace Furball.Vixie.TestApplication.Tests {
    public class TestLineRenderer : GameComponent {
        private LineRenderer _lineRenderer;

        private ImGuiController _imGuiController;

        public TestLineRenderer(Game game) : base(game) {}

        public override void Initialize() {
            this._lineRenderer = new LineRenderer();

            this._imGuiController = ImGuiCreator.CreateController();

            base.Initialize();
        }

        private int CirnoDons = 128;

        public override void Draw(double deltaTime) {
            Global.Gl.Clear(ClearBufferMask.ColorBufferBit);

            this._lineRenderer.Begin();

            this._lineRenderer.Draw(new Vector2(0,0), new Vector2(1280, 720), 4, Color.Red);




            #region ImGui menu

            this._imGuiController.Update((float) deltaTime);

            ImGui.Text($"Frametime: {Math.Round(1000.0f / ImGui.GetIO().Framerate, 2).ToString(CultureInfo.InvariantCulture)} " +
                       $"Framerate: {Math.Round(ImGui.GetIO().Framerate,           2).ToString(CultureInfo.InvariantCulture)}"
            );

            if (ImGui.Button("Go back to test selector")) {
                this.BaseGame.Components.Add(new BaseTestSelector(this.BaseGame));
                this.BaseGame.Components.Remove(this);
            }

            this._imGuiController.Render();

            #endregion

            base.Draw(deltaTime);
        }
    }
}

