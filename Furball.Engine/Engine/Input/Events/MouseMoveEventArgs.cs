using System;
using System.Numerics;

namespace Furball.Engine.Engine.Input.Events; 

public class MouseMoveEventArgs : EventArgs {
    public Vector2      Position;
    public FurballMouse Mouse;

    public MouseMoveEventArgs(Vector2 position, FurballMouse mouse) {
        this.Position = position;
        this.Mouse    = mouse;
    }
}
