// ReSharper disable InconsistentNaming

using System;

namespace Furball.Vixie.Graphics.Backends {
    [Flags]
    public enum Backend {
        None       = 1 << 0, //Not a real backend
        Direct3D11 = 1 << 1,
        OpenGL20   = 1 << 2,
        OpenGL41   = 1 << 3,
        OpenGLES   = 1 << 4,
        Veldrid    = 1 << 5,
    }
}