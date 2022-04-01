using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Threading;
using Furball.Vixie.Graphics.Backends.OpenGL_;
using Furball.Vixie.Graphics.Backends.OpenGL41.Abstractions;
using Furball.Vixie.Graphics.Renderers;
using Furball.Vixie.Helpers;
using Kettu;
using Silk.NET.Core.Native;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;

namespace Furball.Vixie.Graphics.Backends.OpenGL41 {
    // ReSharper disable once InconsistentNaming
    public class OpenGL41Backend : GraphicsBackend, IGLBasedBackend {
        /// <summary>
        /// OpenGL API
        /// </summary>
        // ReSharper disable once InconsistentNaming
        private GL gl;
        /// <summary>
        /// Projection Matrix used to go from Window Coordinates to OpenGL Coordinates
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
        /// <summary>
        /// Stores the Main Thread that OpenGL commands run on, used to ensure that OpenGL commands don't run on different threads
        /// </summary>
        private static Thread _mainThread;
        /// <summary>
        /// Gets the Thread of Operation
        /// </summary>
        [Conditional("DEBUG")]
        private void GetMainThread() {
            _mainThread = Thread.CurrentThread;
        }
        /// <summary>
        /// Ensures that OpenGL commands don't run on the wrong thread
        /// </summary>
        /// <exception cref="ThreadStateException">Throws if a cross-thread operation has occured</exception>
        [Conditional("DEBUG")]
        internal void CheckThread() {
            if (Thread.CurrentThread != _mainThread)
                throw new ThreadStateException("You are calling GL on the wrong thread!");
        }
        /// <summary>
        /// Used to Initialize the Backend
        /// </summary>
        /// <param name="window"></param>
        public override void Initialize(IWindow window) {
            this.GetMainThread();

            this.gl = window.CreateOpenGL();

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
            this.CheckError();
            this.gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            this.CheckError();

            this.ImGuiController = new ImGuiController(this.gl, Global.GameInstance.WindowManager.GameWindow, Global.GameInstance._inputContext);
            this.CheckError();

            Logger.Log($"OpenGL Version: {this.gl.GetStringS(StringName.Version)}",                LoggerLevelOpenGL41.InstanceInfo);
            this.CheckError();
            Logger.Log($"GLSL Version:   {this.gl.GetStringS(StringName.ShadingLanguageVersion)}", LoggerLevelOpenGL41.InstanceInfo);
            this.CheckError();
            Logger.Log($"OpenGL Vendor:  {this.gl.GetStringS(StringName.Vendor)}",                 LoggerLevelOpenGL41.InstanceInfo);
            this.CheckError();
            Logger.Log($"Renderer:       {this.gl.GetStringS(StringName.Renderer)}",               LoggerLevelOpenGL41.InstanceInfo);
            this.CheckError();
            
            this.CheckError();
        }
        public void CheckError(string message = "") {
            this.CheckErrorInternal(message);
        }
        /// <summary>
        /// Checks for OpenGL errors
        /// </summary>
        [Conditional("DEBUG")]
        public void CheckErrorInternal(string message = "") {
            GLEnum error = this.gl.GetError();
            
            if (error != GLEnum.NoError) {
#if DEBUGWITHGL
                throw new Exception($"Got GL Error {error}!");
#else
                Debugger.Break();
                Logger.Log($"OpenGL Error! Code: {error.ToString()}", LoggerLevelOpenGL41.InstanceError);
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
        /// Used to Handle the Window size Changing
        /// </summary>
        /// <param name="width">New width</param>
        /// <param name="height">New height</param>
        public override void HandleWindowSizeChange(int width, int height) {
            this.gl.Viewport(0, 0, (uint) width, (uint) height);

            this.ProjectionMatrix = Matrix4x4.CreateOrthographicOffCenter(0, width, 0, height, 1f, 0f);
        }
        /// <summary>
        /// Used to handle the Framebuffer Resizing
        /// </summary>
        /// <param name="width">New width</param>
        /// <param name="height">New height</param>
        public override void HandleFramebufferResize(int width, int height) {
            this.gl.Viewport(0, 0, (uint) width, (uint) height);
        }
        /// <summary>
        /// Used to Create a Texture Renderer
        /// </summary>
        /// <returns>A Texture Renderer</returns>
        public override IQuadRenderer CreateTextureRenderer() {
            return new QuadRendererGL41(this);
        }
        /// <summary>
        /// Used to Create a Line Renderer
        /// </summary>
        /// <returns></returns>
        public override ILineRenderer CreateLineRenderer() {
            return new LineRendererGL41(this);
        }
        /// <summary>
        /// Gets the Amount of Texture Units available for use
        /// </summary>
        /// <returns>Amount of Texture Units supported</returns>
        public override int QueryMaxTextureUnits() {
            if (this._maxTextureUnits == -1) {
                this.gl.GetInteger(GetPName.MaxTextureImageUnits, out int maxTexSlots);
                this.CheckError();
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
        /// <summary>
        /// Used to Create a TextureRenderTarget
        /// </summary>
        /// <param name="width">Width of the Target</param>
        /// <param name="height">Height of the Target</param>
        /// <returns></returns>
        public override TextureRenderTarget CreateRenderTarget(uint width, uint height) {
            return new TextureRenderTargetGL41(this, width, height);
        }
        /// <summary>
        /// Creates a Texture given some Data
        /// </summary>
        /// <param name="imageData">Image Data</param>
        /// <param name="qoi">Is the Data in the QOI format?</param>
        /// <returns>Texture</returns>
        public override Texture CreateTexture(byte[] imageData, bool qoi = false) {
            return new TextureGL41(this, imageData, qoi);
        }
        /// <summary>
        /// Creates a Texture given a Stream
        /// </summary>
        /// <param name="stream">Stream to read from</param>
        /// <returns>Texture</returns>
        public override Texture CreateTexture(Stream stream) {
            return new TextureGL41(this, stream);
        }
        /// <summary>
        /// Creates a Empty Texture given a Size
        /// </summary>
        /// <param name="width">Width of Texture</param>
        /// <param name="height">Height of Texture</param>
        /// <returns>Texture</returns>
        public override Texture CreateTexture(uint width, uint height) {
            return new TextureGL41(this, width, height);
        }
        /// <summary>
        /// Creates a Texture from a File
        /// </summary>
        /// <param name="filepath">Filepath to Image</param>
        /// <returns>Texture</returns>
        public override Texture CreateTexture(string filepath) {
            return new TextureGL41(this, filepath);
        }
        /// <summary>
        /// Used to Create a 1x1 Texture with only a white pixel
        /// </summary>
        /// <returns>White Pixel Texture</returns>
        public override Texture CreateWhitePixelTexture() {
            return new TextureGL41(this);
        }
        /// <summary>
        /// Used to Update the ImGuiController in charge of rendering ImGui on this backend
        /// </summary>
        /// <param name="deltaTime">Delta Time</param>
        public override void ImGuiUpdate(double deltaTime) {
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
        /// Returns the OpenGL API
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
        public GLBackendType             GetType()     => GLBackendType.Modern;
        public GL                        GetModernGL() => this.gl;
        public Silk.NET.OpenGL.Legacy.GL GetLegacyGL() => throw new WrongGLBackendException();
        public Silk.NET.OpenGLES.GL      GetGLES()     => throw new WrongGLBackendException();
        
        public uint                      GenBuffer()   => this.gl.GenBuffer();
        public void BindBuffer(BufferTargetARB usage, uint buf) {
            this.gl.BindBuffer(usage, buf);
        }
        public unsafe void BufferData(BufferTargetARB bufferType, nuint size, void* data, BufferUsageARB bufferUsage) {
            this.gl.BufferData(bufferType, size, data, bufferUsage);
        }
        public unsafe void BufferSubData(BufferTargetARB bufferType, nint offset, nuint size, void* data) {
            this.gl.BufferSubData(bufferType, offset, size, data);
        }
        public void DeleteBuffer(uint bufferId) {
            this.gl.DeleteBuffer(bufferId);
        }
    }
}