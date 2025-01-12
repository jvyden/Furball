/*
 * This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/.
 *
 * Originally written by Beyley Thomas <ep1cm1n10n123@gmail.com>
 */

using System;
using FFmpeg.AutoGen;

namespace Furball.Engine.Engine.Graphics.Video; 

public unsafe class FFmpegCodecContext : IDisposable{
    public AVCodecContext* CodecContext;
    public FFmpegCodecContext(AVCodecContext* codecContext) {
        this.CodecContext = codecContext;
    }

    private void ReleaseUnmanagedResources() {
        if (this.CodecContext == null)
            return;

        AVCodecContext* context = this.CodecContext;
        ffmpeg.avcodec_free_context(&context);
        this.CodecContext = null;
    }
    public void Dispose() {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }
    ~FFmpegCodecContext() {
        ReleaseUnmanagedResources();
    }
}
