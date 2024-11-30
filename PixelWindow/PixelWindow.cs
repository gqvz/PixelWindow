using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using ErrorCode = OpenTK.Graphics.OpenGL.ErrorCode;

namespace PixelWindow;

public class PixelWindow : GameWindow
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

    private int _vertexBufferObject;
    private int _vertexArrayObject;
    private int _texture;
    private int _shaderProgram;

    private readonly float[] _vertices =
    [
        // Position         // Texture coordinates
         1.0f,  1.0f, 0.0f, 1.0f, 1.0f, // Top right
         1.0f, -1.0f, 0.0f, 1.0f, 0.0f, // Bottom right
        -1.0f, -1.0f, 0.0f, 0.0f, 0.0f, // Bottom left
        -1.0f,  1.0f, 0.0f, 0.0f, 1.0f  // Top left
    ];

    private readonly uint[] _indices =
    {
        0, 1, 3, // First triangle
        1, 2, 3  // Second triangle
    };

    private unsafe byte* _data;

    // language=GLSL
    private const string VertexShaderSource = """
                                              
                                              #version 330 core
                                          
                                              layout(location = 0) in vec3 aPosition;
                                              layout(location = 1) in vec2 aTexCoord;
                                          
                                              out vec2 texCoord;
                                                  
                                              void main()
                                              {
                                                  texCoord = aTexCoord;
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

    public PixelWindow(int width, int height) : base(new GameWindowSettings(),
        new NativeWindowSettings
        {
            ClientSize = new Vector2i(width, height),
            Title = "Ray Tracing in One Weekend"
        })
    {
        unsafe
        {
            GLFW.SetWindowSizeLimits(WindowPtr, width, height, width, height);
        }
    }

    protected override void OnLoad()
    {
        base.OnLoad();

        // Set the clear color
        GL.ClearColor(0.1f, 0.1f, 0.1f, 1.0f);

        // Generate and bind Vertex Array Object
        _vertexArrayObject = GL.GenVertexArray();
        GL.BindVertexArray(_vertexArrayObject);

        // Generate and bind Vertex Buffer Object
        _vertexBufferObject = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vertexBufferObject);
        GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * sizeof(float), _vertices, BufferUsage.StaticDraw);

        // Generate and bind Element Buffer Object
        var elementBufferObject = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, elementBufferObject);
        GL.BufferData(BufferTarget.ElementArrayBuffer, _indices.Length * sizeof(uint), _indices,
            BufferUsage.StaticDraw);

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

        // Link shaders into a program
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

        // Create and bind texture
        _texture = GL.GenTexture();
        UpdateTexture();

        GL.GenerateMipmap(TextureTarget.Texture2d);

        // Bind texture uniform
        GL.UseProgram(_shaderProgram);
        var textureUniformLocation = GL.GetUniformLocation(_shaderProgram, "texture0");
        GL.Uniform1i(textureUniformLocation, 0); // Use texture unit 0
    }

    private unsafe void UpdateTexture()
    {
        GL.BindTexture(TextureTarget.Texture2d, _texture);

        // Set texture parameters
        GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        
        GL.TexImage2D(TextureTarget.Texture2d, 0, InternalFormat.Rgba, ClientSize.X, ClientSize.Y, 0, PixelFormat.Rgba,
            PixelType.UnsignedByte, _data);
    }

    protected override unsafe void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);

        GL.Clear(ClearBufferMask.ColorBufferBit);
        var error = GL.GetError();
        if (error != ErrorCode.NoError)
            Console.WriteLine($"OpenGL Error: {error}");
        GL.UseProgram(_shaderProgram);
        GL.BindVertexArray(_vertexArrayObject);
        GL.BindTexture(TextureTarget.Texture2d, _texture);
        GL.DrawElements(PrimitiveType.Triangles, _indices.Length, DrawElementsType.UnsignedInt, 0);

        SwapBuffers();
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        base.OnResize(e);
        
        GL.Viewport(0, 0, ClientSize.X, ClientSize.Y);
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
