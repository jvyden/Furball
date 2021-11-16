using System;
using System.Numerics;
using Furball.Engine.Engine.Graphics.Drawables;
using Furball.Engine.Engine.Graphics.Drawables.UiElements;
using Furball.Vixie.Input;
using Silk.NET.GLFW;
using Silk.NET.Input;

namespace Furball.Engine.Engine.DevConsole {
    public class ConsoleDrawable : UiTextBoxDrawable {
        public event EventHandler<ConsoleResult> OnCommandFinished;
    
        public ConsoleDrawable() : base(new Vector2(FurballGame.DEFAULT_WINDOW_WIDTH / 2f, FurballGame.DEFAULT_WINDOW_HEIGHT / 2f), FurballGame.DEFAULT_FONT, "", 30, 300) {
            this.OriginType = OriginType.Center;
            this.Visible    = false;

            this.OnCommit                  += this.OnTextCommit;
            Keyboard.GetKeyboard().KeyDown += this.OnKeyDown;
        }

        private void OnKeyDown(IKeyboard keyboard, Key e, int what) {
            if (e == Key.GraveAccent) {
                this.Visible  = !this.Visible;
                this.Selected = !this.Selected;

                this.Text = "";
            }
        }

        private void OnTextCommit(object sender, string text) {
            ConsoleResult result = DevConsole.Run(text);

            this.OnCommandFinished?.Invoke(this, result);

            this.Visible  = false;
            this.Selected = false;
        }

        public override void Dispose() {
            this.OnCommit                  -= this.OnTextCommit;
            Keyboard.GetKeyboard().KeyDown -= this.OnKeyDown;

            base.Dispose();
        }
    }
}
