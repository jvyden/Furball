using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Numerics;
using Furball.Vixie.Backends.OpenGL.Shared;
using Furball.Vixie.Backends.Shared;
using Furball.Vixie.Backends.Shared.Backends;
using Furball.Vixie.Backends.Shared.Renderers;
using Furball.Vixie.Helpers.Helpers;
using Kettu;
using Silk.NET.Core.Native;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL.Legacy;
using Silk.NET.OpenGL.Legacy.Extensions.EXT;
using Silk.NET.OpenGL.Legacy.Extensions.ImGui;
using Silk.NET.Windowing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using BufferTargetARB=Silk.NET.OpenGL.BufferTargetARB;
using BufferUsageARB=Silk.NET.OpenGL.BufferUsageARB;
using FramebufferAttachment=Silk.NET.OpenGL.FramebufferAttachment;
using FramebufferTarget=Silk.NET.OpenGL.FramebufferTarget;
using GetPName=Silk.NET.OpenGL.GetPName;
using InternalFormat=Silk.NET.OpenGL.InternalFormat;
using PixelFormat=Silk.NET.OpenGL.PixelFormat;
using PixelType=Silk.NET.OpenGL.PixelType;
using ProgramPropertyARB=Silk.NET.OpenGL.ProgramPropertyARB;
using Rectangle=SixLabors.ImageSharp.Rectangle;
using RenderbufferTarget=Silk.NET.OpenGL.RenderbufferTarget;
using ShaderType=Silk.NET.OpenGL.ShaderType;
using Texture=Furball.Vixie.Backends.Shared.Texture;
using TextureParameterName=Silk.NET.OpenGL.TextureParameterName;
using TextureTarget=Silk.NET.OpenGL.TextureTarget;
using TextureUnit=Silk.NET.OpenGL.TextureUnit;
using VertexAttribIType=Silk.NET.OpenGL.VertexAttribIType;
using VertexAttribPointerType=Silk.NET.OpenGL.VertexAttribPointerType;

namespace Furball.Vixie.Backends.OpenGL20; 

public class LegacyOpenGLBackend : IGraphicsBackend, IGLBasedBackend {
    private GL gl;

    private ImGuiController _imgui;

    public Matrix4x4 ProjectionMatrix;

    public void CheckError(string message) {
        this.CheckErrorInternal(message);
    }
    public void GlCheckThread() {
        this.CheckThread();
    }

    /// <summary>
    /// Checks for OpenGL errors
    /// </summary>
    /// <param name="erorr"></param>
    [Conditional("DEBUG")]
    private void CheckErrorInternal(string erorr = "") {
        GLEnum error = this.gl.GetError();

        if (error != GLEnum.NoError) {
#if DEBUGWITHGL
                throw new Exception($"Got GL Error {error}!");
#else
            Debugger.Break();
            Logger.Log($"OpenGL Error! Code: {error.ToString()} Extra Info: {erorr}", LoggerLevelOpenGL20.InstanceError);
#endif
        }
    }

    public override void Initialize(IView view, IInputContext inputContext) {
        this.gl = view.CreateLegacyOpenGL();

        //TODO: Lets just assume they have it for now :^)
        this.framebufferObjectEXT = new ExtFramebufferObject(this.gl.Context);

#if DEBUGWITHGL
            unsafe {
                //Enables Debugging
                gl.Enable(EnableCap.DebugOutput);
                gl.Enable(EnableCap.DebugOutputSynchronous);
                gl.DebugMessageCallback(this.Callback, null);
            }
#endif

        //Setup blend mode
        this.gl.Enable(EnableCap.Blend);
        this.gl.BlendFunc(GLEnum.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

        this.gl.Enable(EnableCap.Texture2D);
        this.gl.Enable(EnableCap.ScissorTest);

        this._imgui = new ImGuiController(this.gl, view, inputContext);
            
        BackendInfoSection mainSection = new BackendInfoSection("OpenGL Info");
        mainSection.Contents.Add(("OpenGL Version", this.gl.GetStringS(StringName.Version)));
        mainSection.Contents.Add(("GLSL Version", this.gl.GetStringS(StringName.ShadingLanguageVersion)));
        mainSection.Contents.Add(("OpenGL Vendor", this.gl.GetStringS(StringName.Vendor)));
        mainSection.Contents.Add(("Renderer", this.gl.GetStringS(StringName.Renderer)));
        mainSection.Contents.Add(("Supported Extensions", this.gl.GetStringS(StringName.Extensions)));
        this.InfoSections.Add(mainSection);

        this.InfoSections.ForEach(x => x.Log(LoggerLevelOpenGL20.InstanceInfo));
            
        view.Closing += delegate {
            this.RunImGui = false;
        };
        this._fbSize = new Vector2D<int>(view.Size.X, view.Size.Y);
    }

    /// <summary>
    /// Debug Callback
    /// </summary>
    private void Callback(GLEnum source, GLEnum type, int id, GLEnum severity, int length, nint message, nint userparam) {
        string stringMessage = SilkMarshal.PtrToString(message);

        LoggerLevel level = severity switch {
            GLEnum.DebugSeverityHigh         => LoggerLevelDebugMessageCallback.InstanceHigh,
            GLEnum.DebugSeverityMedium       => LoggerLevelDebugMessageCallback.InstanceMedium,
            GLEnum.DebugSeverityLow          => LoggerLevelDebugMessageCallback.InstanceLow,
            GLEnum.DebugSeverityNotification => LoggerLevelDebugMessageCallback.InstanceNotification,
            _                                => null
        };

        Console.WriteLine(stringMessage);
    }

    public override void Cleanup() {
        this.gl.Dispose();
    }

    public override void HandleFramebufferResize(int width, int height) {
        this.gl.Viewport(0, 0, (uint)width, (uint)height);

        this.VerticalRatio = height / 720f;

        this.ProjectionMatrix = Matrix4x4.CreateOrthographicOffCenter(0, width / (float)height * 720f, 720, 0, 0, 1);
        this._fbSize          = new Vector2D<int>(width, height);
    }

    public override Rectangle ScissorRect {
        get => this._lastScissor;
        set => this.gl.Scissor(value.X, this._fbSize.Y - value.Height - value.Y, (uint)value.Width, (uint)value.Height);
    }
    public override void SetFullScissorRect() {
        this.ScissorRect = new(0, 0, this._fbSize.X, this._fbSize.Y);
    }
    public override IQuadRenderer CreateTextureRenderer() => new QuadRendererGL20(this);
    public override ILineRenderer CreateLineRenderer()    => new BatchedNativeLineRenderer(this);

    private int                  _maxTexUnits = -1;
    private ExtFramebufferObject framebufferObjectEXT;
    private bool                 _screenshotQueued;
    private bool                 RunImGui = true;
    private Vector2D<int>        _fbSize;
    private Rectangle            _lastScissor;
    public override int QueryMaxTextureUnits() {
        if (this._maxTexUnits == -1)
            this._maxTexUnits = this.gl.GetInteger((GLEnum)Silk.NET.OpenGL.Legacy.GetPName.MaxTextureImageUnits);

        return this._maxTexUnits;
    }
    public override void Clear() {
        this.gl.Clear(ClearBufferMask.ColorBufferBit);
        this.gl.ClearColor(0f, 0, 0, 0);
    }
    public override void TakeScreenshot() {
        this._screenshotQueued = true;
    }
    public override unsafe void Present() {
        if (this._screenshotQueued) {
            this._screenshotQueued = false;

            int[] viewport = new int[4];

            this.gl.GetInteger(Silk.NET.OpenGL.Legacy.GetPName.Viewport, viewport);
                
            Rgba32[] colorArr = new Rgba32[viewport[2] * viewport[3]];

            fixed (void* ptr = colorArr)
                this.gl.ReadPixels(viewport[0], viewport[1], (uint)viewport[2], (uint)viewport[3], Silk.NET.OpenGL.Legacy.PixelFormat.Rgba, Silk.NET.OpenGL.Legacy.PixelType.UnsignedByte, ptr);
                
            Image img = Image.LoadPixelData(colorArr, viewport[2], viewport[3]);

            img = img.CloneAs<Rgb24>();
            img.Mutate(x => x.Flip(FlipMode.Vertical));
                
            this.InvokeScreenshotTaken(img);
        }
    }

    public override TextureRenderTarget CreateRenderTarget(uint width, uint height) => new TextureRenderTargetGL(this, width, height);

    public override Texture CreateTexture(byte[] imageData, bool qoi = false) => new TextureGL(this, imageData, qoi);

    public override Texture CreateTexture(Stream stream) => new TextureGL(this, stream);

    public override Texture CreateTexture(uint width, uint height) => new TextureGL(this, width, height);

    public override Texture CreateTexture(string filepath) => new TextureGL(this, filepath);

    public override Texture CreateWhitePixelTexture() => new TextureGL(this);

    [Pure]
    public GL GetOpenGL() => this.gl;
    [Pure]
    public ExtFramebufferObject GetOpenGLFramebufferEXT() => this.framebufferObjectEXT;

    public override void ImGuiUpdate(double deltaTime) {
        if(this.RunImGui)
            this._imgui.Update((float)deltaTime);
    }
    public override void ImGuiDraw(double deltaTime) {
        if(this.RunImGui)
            this._imgui.Render();
    }

    public new GLBackendType GetType() => GLBackendType.Legacy;
    public float VerticalRatio {
        get;
        set;
    }
    public Silk.NET.OpenGL.GL   GetModernGL() => throw new WrongGLBackendException();
    public GL                   GetLegacyGL() => this.gl;
    public Silk.NET.OpenGLES.GL GetGLES()     => throw new WrongGLBackendException();

    public uint GenBuffer() => this.gl.GenBuffer();

    public void BindBuffer(BufferTargetARB usage, uint buf) {
        this.gl.BindBuffer((Silk.NET.OpenGL.Legacy.BufferTargetARB)usage, buf);
    }

    public unsafe void BufferData(BufferTargetARB bufferType, nuint size, void* data, BufferUsageARB bufferUsage) {
        this.gl.BufferData((Silk.NET.OpenGL.Legacy.BufferTargetARB)bufferType, size, data, (Silk.NET.OpenGL.Legacy.BufferUsageARB)bufferUsage);
    }

    public unsafe void BufferSubData(BufferTargetARB bufferType, nint offset, nuint size, void* data) {
        this.gl.BufferSubData((Silk.NET.OpenGL.Legacy.BufferTargetARB)bufferType, offset, size, data);
    }

    public void DeleteBuffer(uint bufferId) {
        this.gl.DeleteBuffer(bufferId);
    }

    public void DeleteFramebuffer(uint frameBufferId) {
        this.framebufferObjectEXT.DeleteFramebuffer(frameBufferId);
    }

    public void DeleteTexture(uint textureId) {
        this.gl.DeleteTexture(textureId);
    }

    public void DeleteRenderbuffer(uint bufId) {
        this.framebufferObjectEXT.DeleteRenderbuffer(bufId);
    }

    public unsafe void DrawBuffers(uint i, in Silk.NET.OpenGL.GLEnum[] drawBuffers) {
        //this isnt pretty, but should work
        fixed (void* ptr = drawBuffers)
            this.gl.DrawBuffers(i, (GLEnum*)ptr);
    }

    public void BindFramebuffer(FramebufferTarget framebuffer, uint frameBufferId) {
        this.framebufferObjectEXT.BindFramebuffer((Silk.NET.OpenGL.Legacy.FramebufferTarget)framebuffer, frameBufferId);
    }

    public uint GenFramebuffer() => this.framebufferObjectEXT.GenFramebuffer();

    public void BindTexture(TextureTarget target, uint textureId) {
        this.gl.BindTexture((Silk.NET.OpenGL.Legacy.TextureTarget)target, textureId);
    }
    public void BindTextures(uint[] textures, uint count) {
        for (int i = 0; i < count; i++) {
            uint texture = textures[i];
                
            this.ActiveTexture(TextureUnit.Texture0 + i);
            this.BindTexture(TextureTarget.Texture2D, texture);
        }
    }

    public unsafe void TexImage2D(TextureTarget target, int level, InternalFormat format, uint width, uint height, int border, PixelFormat pxFormat, PixelType type, void* data) {
        this.gl.TexImage2D((Silk.NET.OpenGL.Legacy.TextureTarget)target, level, (Silk.NET.OpenGL.Legacy.InternalFormat)format, width, height, border, (Silk.NET.OpenGL.Legacy.PixelFormat)pxFormat, (Silk.NET.OpenGL.Legacy.PixelType)type, data);
    }

    public void TexParameterI(TextureTarget target, Silk.NET.OpenGL.GLEnum param, int paramData) {
        this.gl.TexParameterI((Silk.NET.OpenGL.Legacy.TextureTarget)target, (GLEnum)param, paramData);
    }

    public uint GenRenderbuffer() => this.framebufferObjectEXT.GenRenderbuffer();

    public void Viewport(int x, int y, uint width, uint height) {
        this.gl.Viewport(x, y, width, height);
    }

    public uint GenTexture() => this.gl.GenTexture();

    public void BindRenderbuffer(RenderbufferTarget target, uint id) {
        this.framebufferObjectEXT.BindRenderbuffer((EXT)target, id);
    }

    public void RenderbufferStorage(RenderbufferTarget target, InternalFormat format, uint width, uint height) {
        this.framebufferObjectEXT.RenderbufferStorage((EXT)target, (EXT)format, width, height);
    }

    public void FramebufferRenderbuffer(FramebufferTarget target, FramebufferAttachment attachment, RenderbufferTarget rbTarget, uint id) {
        this.framebufferObjectEXT.FramebufferRenderbuffer((Silk.NET.OpenGL.Legacy.FramebufferTarget)target, (Silk.NET.OpenGL.Legacy.FramebufferAttachment)attachment, (Silk.NET.OpenGL.Legacy.RenderbufferTarget)rbTarget, id);
    }

    public void FramebufferTexture(FramebufferTarget target, FramebufferAttachment colorAttachment0, uint textureId, int level) {
        this.framebufferObjectEXT.FramebufferTexture2D((Silk.NET.OpenGL.Legacy.FramebufferTarget)target, (Silk.NET.OpenGL.Legacy.FramebufferAttachment)colorAttachment0, Silk.NET.OpenGL.Legacy.TextureTarget.Texture2D, textureId, level);
    }

    public Silk.NET.OpenGL.GLEnum CheckFramebufferStatus(FramebufferTarget target) => (Silk.NET.OpenGL.GLEnum)this.framebufferObjectEXT.CheckFramebufferStatus((EXT)target);

    public void GetInteger(GetPName viewport, ref int[] oldViewPort) {
        this.gl.GetInteger((GLEnum)viewport, oldViewPort);
    }

    public void TexParameter(TextureTarget target, TextureParameterName paramName, int param) {
        this.gl.TexParameter((GLEnum)target, (GLEnum)paramName, param);
    }

    public unsafe void TexSubImage2D(TextureTarget target, int level, int x, int y, uint width, uint height, PixelFormat pxformat, PixelType pxtype, void* data) {
        this.gl.TexSubImage2D((GLEnum)target, level, x, y, width, height, (GLEnum)pxformat, (GLEnum)pxtype, data);
    }

    public void ActiveTexture(TextureUnit textureSlot) {
        this.gl.ActiveTexture((GLEnum)textureSlot);
    }

    public uint CreateProgram() => this.gl.CreateProgram();

    public uint CreateShader(ShaderType type) => this.gl.CreateShader((GLEnum)type);

    public void ShaderSource(uint shaderId, string source) {
        this.gl.ShaderSource(shaderId, source);
    }

    public void CompileShader(uint shaderId) {
        this.gl.CompileShader(shaderId);
    }

    public string GetShaderInfoLog(uint shaderId) => this.gl.GetShaderInfoLog(shaderId);

    public void AttachShader(uint programId, uint shaderId) {
        this.gl.AttachShader(programId, shaderId);
    }

    public void LinkProgram(uint programId) {
        this.gl.LinkProgram(programId);
    }

    public void GetProgram(uint programId, ProgramPropertyARB linkStatus, out int i) {
        this.gl.GetProgram(programId, (GLEnum)linkStatus, out i);
    }

    public void DeleteShader(uint shader) {
        this.gl.DeleteShader(shader);
    }

    public string GetProgramInfoLog(uint programId) => this.gl.GetProgramInfoLog(programId);

    public void UseProgram(uint programId) {
        this.gl.UseProgram(programId);
    }

    public int GetUniformLocation(uint programId, string uniformName) => this.gl.GetUniformLocation(programId, uniformName);

    public unsafe void UniformMatrix4(int getUniformLocation, uint i, bool b, float* f) {
        this.gl.UniformMatrix4(getUniformLocation, i, b, f);
    }

    public void Uniform1(int getUniformLocation, float f) {
        this.gl.Uniform1(getUniformLocation, f);
    }

    public void Uniform1(int getUniformLocation, int f) {
        this.gl.Uniform1(getUniformLocation, f);
    }

    public void Uniform2(int getUniformLocation, float f, float f2) {
        this.gl.Uniform2(getUniformLocation, f, f2);
    }

    public void Uniform2(int getUniformLocation, int f, int f2) {
        this.gl.Uniform2(getUniformLocation, f, f2);
    }

    public void DeleteProgram(uint programId) {
        this.gl.DeleteProgram(programId);
    }

    public void DeleteVertexArray(uint arrayId) {
        this.gl.DeleteVertexArray(arrayId);
    }

    public uint GenVertexArray() => this.gl.GenVertexArray();

    public void EnableVertexAttribArray(uint u) {
        this.gl.EnableVertexAttribArray(u);
    }

    public unsafe void VertexAttribPointer(uint u, int currentElementCount, VertexAttribPointerType currentElementType, bool currentElementNormalized, uint getStride, void* offset) {
        this.gl.VertexAttribPointer(u, currentElementCount, (GLEnum)currentElementType, currentElementNormalized, getStride, offset);
    }

    public unsafe void VertexAttribIPointer(uint u, int currentElementCount, VertexAttribIType vertexAttribIType, uint getStride, void* offset) {
        this.gl.VertexAttribIPointer(u, currentElementCount, (GLEnum)vertexAttribIType, getStride, offset);
    }

    public void BindVertexArray(uint arrayId) {
        this.gl.BindVertexArray(arrayId);
    }
}