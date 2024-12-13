using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace PixelWindow;

public class PixelWindow(NativeWindowSettings settings) : GameWindow(new GameWindowSettings(), settings )
{
    public unsafe byte* Data
    {
        get => _data;
        set
        {
            _data = value;
            UpdateTexture();
        }
    }

    public float Zoom { get; set; } = 1f;

    public Vector2 ZoomOffset { get; set; }

    public Vector2i TextureSize { get; set; }

    private int _vertexBufferObject;
    private int _vertexArrayObject;
    private int _texture;
    private int _shaderProgram;

    private readonly uint[] _indices =
    [
        0, 1, 3, 
        1, 2, 3
    ];

    private readonly float[] _vertices =
    [
        -1.0f, -1.0f, 0.0f, 0.0f, 0.0f,
        1.0f, -1.0f, 0.0f, 1.0f, 0.0f,
        1.0f, 1.0f, 0.0f, 1.0f, 1.0f,
        -1.0f, 1.0f, 0.0f, 0.0f, 1.0f
    ];

    private unsafe byte* _data;

    // language=GLSL
    private const string VertexShaderSource = """

                                              #version 330 core

                                              layout(location = 0) in vec3 aPosition;
                                              layout(location = 1) in vec2 aTexCoord;

                                              out vec2 texCoord;

                                              uniform float zoom;
                                              uniform vec2 zoomOffset;
                                                  
                                              void main()
                                              {
                                                  texCoord = (aTexCoord - 0.5) / zoom + 0.5 + zoomOffset;
                                                  gl_Position = vec4(aPosition, 1.0);
                                              }
                                                  
                                              """;

    // language=GLSL
    private const string FragmentShaderSource = """

                                                #version 330 core

                                                in vec2 texCoord;
                                                out vec4 outputColor;

                                                uniform sampler2D texture0;

                                                void main()
                                                {
                                                    outputColor = texture(texture0, texCoord);
                                                }
                                                    
                                                """;

    protected override unsafe void OnLoad()
    {
        base.OnLoad();

        GL.ClearColor(0.1f, 0.1f, 0.1f, 1.0f);

        _vertexArrayObject = GL.GenVertexArray();
        GL.BindVertexArray(_vertexArrayObject);

        _vertexBufferObject = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBufferObject);
        GL.BufferData(BufferTarget.ArrayBuffer, 20 * sizeof(float), _vertices, BufferUsage.DynamicDraw);

        var elementBufferObject = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, elementBufferObject);
        GL.BufferData(BufferTarget.ElementArrayBuffer, 6 * sizeof(uint), _indices, BufferUsage.StaticDraw);

        // Set vertex attribute pointers
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), 0);
        GL.EnableVertexAttribArray(0);

        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 5 * sizeof(float), 3 * sizeof(float));
        GL.EnableVertexAttribArray(1);

        // Compile shaders
        var vertexShader = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vertexShader, VertexShaderSource);
        GL.CompileShader(vertexShader);

        GL.GetShaderi(vertexShader, ShaderParameterName.CompileStatus, out var success);
        if (success == 0)
        {
            GL.GetShaderInfoLog(vertexShader, out var infoLog);
            Console.WriteLine($"Vertex Shader Compilation Error: {infoLog}");
        }

        var fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(fragmentShader, FragmentShaderSource);
        GL.CompileShader(fragmentShader);

        GL.GetShaderi(fragmentShader, ShaderParameterName.CompileStatus, out success);
        if (success == 0)
        {
            GL.GetShaderInfoLog(fragmentShader, out var infoLog);
            Console.WriteLine($"Fragment Shader Compilation Error: {infoLog}");
        }

        _shaderProgram = GL.CreateProgram();
        GL.AttachShader(_shaderProgram, vertexShader);
        GL.AttachShader(_shaderProgram, fragmentShader);
        GL.LinkProgram(_shaderProgram);

        GL.GetProgrami(_shaderProgram, ProgramProperty.LinkStatus, out success);
        if (success == 0)
        {
            GL.GetProgramInfoLog(_shaderProgram, out var infoLog);
            Console.WriteLine($"Shader Program Linking Error: {infoLog}");
        }

        GL.DetachShader(_shaderProgram, vertexShader);
        GL.DetachShader(_shaderProgram, fragmentShader);
        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);

        _texture = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2d, _texture);
        GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);        GL.TexImage2D(TextureTarget.Texture2d, 0, InternalFormat.Rgba, TextureSize.X, TextureSize.Y, 0,
            PixelFormat.Rgba,
            PixelType.UnsignedByte, _data);

        GL.GenerateMipmap(TextureTarget.Texture2d);

        GL.UseProgram(_shaderProgram);
        var textureUniformLocation = GL.GetUniformLocation(_shaderProgram, "texture0");
        GL.Uniform1i(textureUniformLocation, 0);

        var zoomUniformLocation = GL.GetUniformLocation(_shaderProgram, "zoom");
        GL.Uniform1f(zoomUniformLocation, Zoom);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        GL.BindTexture(TextureTarget.Texture2d, _texture);

        var zoomUniformLocation = GL.GetUniformLocation(_shaderProgram, "zoom");
        GL.Uniform1f(zoomUniformLocation, Zoom);

        var zoomOffsetUniformLocation = GL.GetUniformLocation(_shaderProgram, "zoomOffset");
        GL.Uniform2f(zoomOffsetUniformLocation, ZoomOffset.X, ZoomOffset.Y);
        
        UpdateTexture();
    }


    public unsafe void UpdateTexture()
    {
        GL.BindTexture(TextureTarget.Texture2d, _texture);
        GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        GL.TexImage2D(TextureTarget.Texture2d, 0, InternalFormat.Rgba, TextureSize.X, TextureSize.Y, 0,
            PixelFormat.Rgba,
            PixelType.UnsignedByte, _data);
        GL.DrawElements(PrimitiveType.Triangles, _indices.Length, DrawElementsType.UnsignedInt, 0);
        SwapBuffers();
    }

    protected override unsafe void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);

        GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
        GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        GL.BindTexture(TextureTarget.Texture2d, _texture);
        GL.TexImage2D(TextureTarget.Texture2d, 0, InternalFormat.Rgba, TextureSize.X, TextureSize.Y, 0,
            PixelFormat.Rgba,
            PixelType.UnsignedByte, _data);

        GL.GenerateMipmap(TextureTarget.Texture2d);

        GL.UseProgram(_shaderProgram);
        var textureUniformLocation = GL.GetUniformLocation(_shaderProgram, "texture0");
        GL.Uniform1i(textureUniformLocation, 0);

        var zoomUniformLocation = GL.GetUniformLocation(_shaderProgram, "zoom");
        GL.Uniform1f(zoomUniformLocation, Zoom);
    }

    protected override void OnUnload()
    {
        base.OnUnload();

        GL.DeleteTexture(_texture);
        GL.DeleteBuffer(_vertexBufferObject);
        GL.DeleteVertexArray(_vertexArrayObject);
        GL.DeleteProgram(_shaderProgram);
    }
}
