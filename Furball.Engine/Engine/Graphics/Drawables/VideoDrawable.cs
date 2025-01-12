using System;
using System.IO;
using System.Numerics;
using Furball.Engine.Engine.Graphics.Video;
using Furball.Engine.Engine.Timing;
using Furball.Vixie;

namespace Furball.Engine.Engine.Graphics.Drawables; 

public class VideoDrawable : TexturedDrawable {
    public double StartTime;

    private readonly ITimeSource _videoTimeSource;

    private readonly VideoDecoder _decoder;

    /// <summary>
    ///     Creates a new video drawable
    /// </summary>
    /// <param name="path">Path to the video file</param>
    /// <param name="speed">The intended playback speed (does *not* actually effect playback speed)</param>
    /// <param name="timeSource">The time source for the video to follow</param>
    /// <param name="position">The position on screen</param>
    public VideoDrawable(byte[] path, double speed, ITimeSource timeSource, Vector2 position) : base(null, position) {
        //If we are moving at twice the speed, we should double our buffer size, and conversely if we are going half the speed we should half our buffer size
        this._decoder = new VideoDecoder((int)(4d * speed));
        this._decoder.Load(new MemoryStream(path));

        //Create the texture which will store our video
        this.Texture      = Texture.CreateEmptyTexture((uint)this._decoder.Width, (uint)this._decoder.Height);
        this.Texture.Name = $"video:{path}";
        
        this._videoTimeSource = timeSource;

        this.StartTime = this._videoTimeSource.GetCurrentTime();
    }

    private double _lastSeek = -1;

    /// <summary>
    ///     Seems the video decoder to the specified time (prevents speedup and such)
    /// </summary>
    /// <param name="milis">The time in miliseconds</param>
    /// <param name="force">Force the seek</param>
    public void Seek(double milis, bool force = false) {
        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (this._lastSeek != -1 && Math.Abs(milis - this._lastSeek) < 2000 && !force) return;

        this._lastSeek = milis;
            
        this._decoder.Seek(milis);
    }

    public override void Update(double time) {
        base.Update(time);

        byte[] data = this._decoder.GetFrame((int)(this._videoTimeSource.GetCurrentTime() - this.StartTime));
        if (data != null)
            this.Texture.SetData(data);
    }

    public override void Dispose() {
        base.Dispose();

        this._decoder.Dispose();
    }
}