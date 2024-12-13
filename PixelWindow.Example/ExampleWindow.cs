using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace PixelWindow.Example;

public class ExampleWindow() : PixelWindow(new NativeWindowSettings
{
    ClientSize = new Vector2i(800, 600),
    Title = "PixelWindow Example",
    IsEventDriven = true
})
{
    private byte[] _data = new byte[800 * 600 * 4];
    private int _width = 800;
    private int _height = 600;

    protected override unsafe void OnLoad()
    {
        base.OnLoad();
        TextureSize = new Vector2i(800, 600); 
        Random.Shared.NextBytes(_data);
        fixed (byte* ptr = _data)
        {
            Data = ptr;
        }
    }

    protected override void OnResize(ResizeEventArgs e)
    {
        TextureSize = new Vector2i(_width = e.Width, _height = e.Height);
        _data = new byte[_width * _height * 4];
        Random.Shared.NextBytes(_data);
        base.OnResize(e);
        UpdateTexture();
    }
    
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        var zoomSensitivity = 0.1f;

        var mouse = MouseState.Position;
        var cursorNdc = new Vector2(
            mouse.X / ClientSize.X * 2 - 1,
            1 - mouse.Y / ClientSize.Y * 2
        );

        Zoom = Math.Max(Zoom + e.OffsetY * zoomSensitivity, 1.0f);

        var screenCenter = Vector2.Zero;
        var zoomCenter = Vector2.Lerp(cursorNdc, screenCenter, 0.5f);

        ZoomOffset = zoomCenter * (1 - 1 / Zoom);
        base.OnMouseWheel(e);
    }
}