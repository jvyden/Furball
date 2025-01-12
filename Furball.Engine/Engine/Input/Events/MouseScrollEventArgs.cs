using System;
using System.Numerics;

namespace Furball.Engine.Engine.Input.Events; 

public class MouseScrollEventArgs : EventArgs {
    public Vector2      ScrollAmount;
    public FurballMouse Mouse;

    public MouseScrollEventArgs(Vector2 scrollAmount, FurballMouse mouse) {
        this.ScrollAmount = scrollAmount;
        this.Mouse        = mouse;
    }
}
