using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using Silk.NET.OpenGLES;
using Silk.NET.OpenGLES.Extensions.ImGui;
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

namespace Furball.Vixie.Backends.OpenGLES {
    // ReSharper disable once InconsistentNaming
    public class OpenGLESBackend : IGraphicsBackend, IGLBasedBackend {
        /// <summary>
        /// OpenGLES API
        /// </summary>
        // ReSharper disable once InconsistentNaming
        private GL gl;
        /// <summary>
        /// Projection Matrix used to go from Window Coordinates to OpenGLES Coordinates
        /// </summary>
        internal Matrix4x4 ProjectionMatrix;
        /// <summary>
        /// Cache for the Maximum amount of Texture units allowed by the device
        /// </summary>
        private int _maxTextureUnits = -1;
        /// <summary>
        /// ImGui Controller
        /// </summary>
        internal ImGuiController ImGuiController;
        internal IView       View;
        private  bool          _screenshotQueued;
        private  bool          RunImGui = true;
        private  Rectangle     _lastScissor;
        private  Vector2D<int> _fbSize;

        public static Dictionary<string, FeatureLevel> FeatureLevels = new() {
            {
                "geometry_shader_lines", new FeatureLevel {
                    Name        = "Geometry Shader Lines", 
                    Description = "Whether to use Geometry Shaders to draw lines", 
                    Value       = false
                }
            }
        };

        public OpenGLESBackend() {
            if (Global.LatestSupportedGL.GLES.MajorVersion >= 3 && Global.LatestSupportedGL.GLES.MinorVersion >= 2) {
                FeatureLevels["geometry_shader_lines"].Value = true;
            }
        }

        /// <summary>
        /// Used to Initialize the Backend
        /// </summary>
        /// <param name="view"></param>
        /// <param name="inputContext"></param>
        /// <param name="game"></param>
        public override void Initialize(IView view, IInputContext inputContext) {
            this.gl     = view.CreateOpenGLES();
            this.View = view;
            
#if DEBUGWITHGL
            unsafe {
                //Enables Debugging
                gl.Enable(EnableCap.DebugOutput);
                gl.Enable(EnableCap.DebugOutputSynchronous);
                gl.DebugMessageCallback(this.Callback, null);
            }
#endif

            //Enables Blending (Required for Transparent Objects)
            this.gl.Enable(EnableCap.Blend);
            this.gl.BlendFunc(GLEnum.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            this.gl.Enable(EnableCap.ScissorTest);

            this.ImGuiController = new ImGuiController(this.gl, view, inputContext);

            BackendInfoSection mainSection = new BackendInfoSection("OpenGL Info");
            mainSection.Contents.Add(("OpenGL Version", this.gl.GetStringS(StringName.Version)));
            mainSection.Contents.Add(("GLSL Version", this.gl.GetStringS(StringName.ShadingLanguageVersion)));
            mainSection.Contents.Add(("OpenGL Vendor", this.gl.GetStringS(StringName.Vendor)));
            mainSection.Contents.Add(("Renderer", this.gl.GetStringS(StringName.Renderer)));
            mainSection.Contents.Add(("Supported Extensions", this.gl.GetStringS(StringName.Extensions)));
            this.InfoSections.Add(mainSection);
            
            this.InfoSections.ForEach(x => x.Log(LoggerLevelOpenGLES.InstanceInfo));
            
            view.Closing += delegate {
                this.RunImGui = false;
            };

            this._fbSize = new Vector2D<int>(view.Size.X, view.Size.Y);
        }
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
                Logger.Log($"OpenGL Error! Code: {error.ToString()} Extra Info: {erorr}", LoggerLevelOpenGLES.InstanceError);
#endif
            }
        }
        /// <summary>
        /// Used to Cleanup the Backend
        /// </summary>
        public override void Cleanup() {
            this.gl.Dispose();
        }
        /// <summary>
        /// Used to handle the Framebuffer Resizing
        /// </summary>
        /// <param name="width">New width</param>
        /// <param name="height">New height</param>
        public override void HandleFramebufferResize(int width, int height) {
            this.gl.Viewport(0, 0, (uint)width, (uint)height);

            this.VerticalRatio = height / 720f;

            this._fbSize          = new Vector2D<int>(width, height);
            this.ProjectionMatrix = Matrix4x4.CreateOrthographicOffCenter(0, width / (float)height * 720f, 720, 0, 1f, 0f);
        }
        /// <summary>
        /// Used to Create a Texture Renderer
        /// </summary>
        /// <returns>A Texture Renderer</returns>
        public override IQuadRenderer CreateTextureRenderer() {
            return new QuadRendererGLES(this);
        }
        /// <summary>
        /// Used to Create a Line Renderer
        /// </summary>
        /// <returns></returns>
        public override ILineRenderer CreateLineRenderer() {
            return FeatureLevels["geometry_shader_lines"].Boolean ? new LineRendererGLESGeometryShaders(this) : new LineRendererGLESLinePrimitiveBatcher(this);
        }
        /// <summary>
        /// Gets the Amount of Texture Units available for use
        /// </summary>
        /// <returns>Amount of Texture Units supported</returns>
        public override int QueryMaxTextureUnits() {
            if (this._maxTextureUnits == -1) {
                this.gl.GetInteger(Silk.NET.OpenGLES.GetPName.MaxTextureImageUnits, out int maxTexSlots);
                this._maxTextureUnits = maxTexSlots;
            }

            return this._maxTextureUnits;
        }
        /// <summary>
        /// Clears the Screen
        /// </summary>
        public override void Clear() {
            this.gl.Clear(ClearBufferMask.ColorBufferBit);
        }
        public override void TakeScreenshot() {
            this._screenshotQueued = true;
        }
        public override unsafe void Present() {
            if (this._screenshotQueued) {
                this._screenshotQueued = false;

                int[] viewport = new int[4];

                this.gl.GetInteger(Silk.NET.OpenGLES.GetPName.Viewport, viewport);
                
                Rgba32[] colorArr = new Rgba32[viewport[2] * viewport[3]];

                fixed (void* ptr = colorArr)
                    this.gl.ReadPixels(viewport[0], viewport[1], (uint)viewport[2], (uint)viewport[3], Silk.NET.OpenGLES.PixelFormat.Rgba, Silk.NET.OpenGLES.PixelType.UnsignedByte, ptr);
                
                Image img = Image.LoadPixelData(colorArr, viewport[2], viewport[3]);

                img = img.CloneAs<Rgb24>();
                img.Mutate(x => x.Flip(FlipMode.Vertical));
                
                this.InvokeScreenshotTaken(img);
            }
        }
        public override Rectangle ScissorRect {
            get => this._lastScissor;
            set => this.gl.Scissor(value.X, this._fbSize.Y - value.Height - value.Y, (uint)value.Width, (uint)value.Height);
        }
        public override void SetFullScissorRect() {
            this.ScissorRect = new(0, 0, this._fbSize.X, this._fbSize.Y);
        }
        /// <summary>
        /// Used to Create a TextureRenderTarget
        /// </summary>
        /// <param name="width">Width of the Target</param>
        /// <param name="height">Height of the Target</param>
        /// <returns></returns>
        public override TextureRenderTarget CreateRenderTarget(uint width, uint height) {
            return new TextureRenderTargetGL(this, width, height);
        }
        /// <summary>
        /// Creates a Texture given some Data
        /// </summary>
        /// <param name="imageData">Image Data</param>
        /// <param name="qoi">Is the Data in the QOI format?</param>
        /// <returns>Texture</returns>
        public override Texture CreateTexture(byte[] imageData, bool qoi = false) {
            return new TextureGL(this, imageData, qoi);
        }
        /// <summary>
        /// Creates a Texture given a Stream
        /// </summary>
        /// <param name="stream">Stream to read from</param>
        /// <returns>Texture</returns>
        public override Texture CreateTexture(Stream stream) {
            return new TextureGL(this, stream);
        }
        /// <summary>
        /// Creates a Empty Texture given a Size
        /// </summary>
        /// <param name="width">Width of Texture</param>
        /// <param name="height">Height of Texture</param>
        /// <returns>Texture</returns>
        public override Texture CreateTexture(uint width, uint height) {
            return new TextureGL(this, width, height);
        }
        /// <summary>
        /// Creates a Texture from a File
        /// </summary>
        /// <param name="filepath">Filepath to Image</param>
        /// <returns>Texture</returns>
        public override Texture CreateTexture(string filepath) {
            return new TextureGL(this, filepath);
        }
        /// <summary>
        /// Used to Create a 1x1 Texture with only a white pixel
        /// </summary>
        /// <returns>White Pixel Texture</returns>
        public override Texture CreateWhitePixelTexture() {
            return new TextureGL(this);
        }
        /// <summary>
        /// Used to Update the ImGuiController in charge of rendering ImGui on this backend
        /// </summary>
        /// <param name="deltaTime">Delta Time</param>
        public override void ImGuiUpdate(double deltaTime) {
            if(this.RunImGui)
                this.ImGuiController.Update((float)deltaTime);
        }
        /// <summary>
        /// Used to Draw the ImGuiController in charge of rendering ImGui on this backend
        /// </summary>
        /// <param name="deltaTime">Delta Time</param>
        public override void ImGuiDraw(double deltaTime) {
            this.ImGuiController.Render();
        }
        /// <summary>
        /// Returns the OpenGLES API
        /// </summary>
        /// <returns></returns>
        public GL GetGlApi() => this.gl;
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
        public GLBackendType             GetType()     => GLBackendType.ES;
        public float VerticalRatio {
            get;
            set;
        }
        public Silk.NET.OpenGL.GL        GetModernGL() => throw new WrongGLBackendException();
        public Silk.NET.OpenGL.Legacy.GL GetLegacyGL() => throw new WrongGLBackendException();
        public GL                        GetGLES()     => this.gl;

        public uint GenBuffer() => this.gl.GenBuffer();

        public void BindBuffer(BufferTargetARB usage, uint buf) {
            this.gl.BindBuffer((Silk.NET.OpenGLES.BufferTargetARB)usage, buf);
        }

        public unsafe void BufferData(BufferTargetARB bufferType, nuint size, void* data, BufferUsageARB bufferUsage) {
            this.gl.BufferData((Silk.NET.OpenGLES.BufferTargetARB)bufferType, size, data, (Silk.NET.OpenGLES.BufferUsageARB)bufferUsage);
        }

        public unsafe void BufferSubData(BufferTargetARB bufferType, nint offset, nuint size, void* data) {
            this.gl.BufferSubData((Silk.NET.OpenGLES.BufferTargetARB)bufferType, offset, size, data);
        }

        public void DeleteBuffer(uint bufferId) {
            this.gl.DeleteBuffer(bufferId);
        }

        public void DeleteFramebuffer(uint frameBufferId) {
            this.gl.DeleteFramebuffer(frameBufferId);
        }

        public void DeleteTexture(uint textureId) {
            this.gl.DeleteTexture(textureId);
        }

        public void DeleteRenderbuffer(uint bufId) {
            this.gl.DeleteRenderbuffer(bufId);
        }

        public unsafe void DrawBuffers(uint i, in Silk.NET.OpenGL.GLEnum[] drawBuffers) {
            //this isnt pretty, but should work
            fixed (void* ptr = drawBuffers)
                this.gl.DrawBuffers(i, (GLEnum*)ptr);
        }

        public void BindFramebuffer(FramebufferTarget framebuffer, uint frameBufferId) {
            this.gl.BindFramebuffer((GLEnum)framebuffer, frameBufferId);
        }

        public uint GenFramebuffer() => this.gl.GenFramebuffer();

        public void BindTexture(TextureTarget target, uint textureId) {
            this.gl.BindTexture((Silk.NET.OpenGLES.TextureTarget)target, textureId);
        }
        public void BindTextures(uint[] textures, uint count) {
            for (int i = 0; i < count; i++) {
                uint texture = textures[i];
                
                this.ActiveTexture(TextureUnit.Texture0 + i);
                this.BindTexture(TextureTarget.Texture2D, texture);
            }
        }

        public unsafe void TexImage2D(TextureTarget target, int level, InternalFormat format, uint width, uint height, int border, PixelFormat pxFormat, PixelType type, void* data) {
            this.gl.TexImage2D((GLEnum)target, level, (Silk.NET.OpenGLES.InternalFormat)format, width, height, border, (GLEnum)pxFormat, (GLEnum)type, data);
        }

        public void TexParameterI(TextureTarget target, Silk.NET.OpenGL.GLEnum param, int paramData) {
            this.gl.TexParameterI((GLEnum)target, (GLEnum)param, paramData);
        }

        public uint GenRenderbuffer() => this.gl.GenRenderbuffer();

        public void Viewport(int x, int y, uint width, uint height) {
            this.gl.Viewport(x, y, width, height);
        }

        public uint GenTexture() => this.gl.GenTexture();

        public void BindRenderbuffer(RenderbufferTarget target, uint id) {
            this.gl.BindRenderbuffer((GLEnum)target, id);
        }

        public void RenderbufferStorage(RenderbufferTarget target, InternalFormat format, uint width, uint height) {
            this.gl.RenderbufferStorage((GLEnum)target, (GLEnum)format, width, height);
        }

        public void FramebufferRenderbuffer(FramebufferTarget target, FramebufferAttachment attachment, RenderbufferTarget rbTarget, uint id) {
            this.gl.FramebufferRenderbuffer((GLEnum)target, (GLEnum)attachment, (GLEnum)rbTarget, id);
        }

        public void FramebufferTexture(FramebufferTarget target, FramebufferAttachment colorAttachment0, uint textureId, int level) {
            this.gl.FramebufferTexture((GLEnum)target, (GLEnum)colorAttachment0, textureId, level);
        }

        public Silk.NET.OpenGL.GLEnum CheckFramebufferStatus(FramebufferTarget target) => (Silk.NET.OpenGL.GLEnum)this.gl.CheckFramebufferStatus((GLEnum)target);

        public void GetInteger(GetPName viewport, ref int[] oldViewPort) {
            this.gl.GetInteger((GLEnum)viewport, oldViewPort);
        }

        public void TexParameter(TextureTarget target, TextureParameterName paramName, int param) {
            this.gl.TexParameter((GLEnum)target, (GLEnum)paramName, param);
        }

        public unsafe void TexSubImage2D(TextureTarget target, int level, int x, int y, uint width, uint height, PixelFormat pxformat, PixelType pxtype, void* data) {
            this.gl.TexSubImage2D((GLEnum)target, level, x, y, width, height, (GLEnum)pxformat, (GLEnum)pxtype, data);
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

        public void Uniform2(int getUniformLocation, float f, float f2) {
            this.gl.Uniform2(getUniformLocation, f, f2);
        }

        public void Uniform1(int getUniformLocation, int f) {
            this.gl.Uniform1(getUniformLocation, f);
        }

        public void Uniform2(int getUniformLocation, int f, int f2) {
            this.gl.Uniform2(getUniformLocation, f, f2);
        }

        public void DeleteProgram(uint programId) {
            this.gl.DeleteProgram(programId);
        }

        public void ActiveTexture(TextureUnit textureSlot) {
            this.gl.ActiveTexture((GLEnum)textureSlot);
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
}
