

namespace Furball.Engine.Engine.Debug.DebugCounter.Items {
    /// <summary>
    /// Displays the Current Game Time as provided by FurballGame.GameTimeSource
    /// </summary>
    public class GameTimeSourceTime : DebugCounterItem {
        public override string GetAsString(double time) => $"gt: {FurballGame.GameTimeSource.GetCurrentTime()}";
    }
}
