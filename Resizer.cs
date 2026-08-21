using ImageMagick;

namespace PixelMatcher;

public enum ResizeMode { None, Stretch, Fit, Cover }

public static class Resizer
{
    public static void Resize(MagickImage image, uint width, uint height, ResizeMode mode)
    {
        if (image.Width == width && image.Height == height)
            return;

        switch (mode)
        {
            case ResizeMode.None:
                {
                    break;
                }
            case ResizeMode.Stretch:
                {
                    image.Resize(new MagickGeometry(width, height) { IgnoreAspectRatio = true });
                    break;
                }
            case ResizeMode.Fit:
                {
                    var scale = Math.Min((double)width / image.Width, (double)height / image.Height);
                    var newWidth = Math.Clamp((uint)Math.Round(image.Width * scale), 1u, width);
                    var newHeight = Math.Clamp((uint)Math.Round(image.Height * scale), 1u, height);
                    image.Resize(new MagickGeometry(newWidth, newHeight) { IgnoreAspectRatio = true });
                    break;
                }
            case ResizeMode.Cover:
                {
                    var scale = Math.Max((double)width / image.Width, (double)height / image.Height);
                    var newWidth = Math.Max(1u, (uint)Math.Round(image.Width * scale));
                    var newHeight = Math.Max(1u, (uint)Math.Round(image.Height * scale));
                    image.Resize(new MagickGeometry(newWidth, newHeight) { IgnoreAspectRatio = true });
                    break;
                }
        }
    }
}
