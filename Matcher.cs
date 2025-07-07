using ImageMagick;

namespace PixelMatcher;

public class Matcher(params MagickImage[] images)
{
    public MagickImage[] Images { get; } = images;

    public MatchResult[] Match()
    {
        var baseImage = Images[0];
        var basePixels = baseImage.GetPixels();
        var results = new MatchResult[Images.Length - 1];

        for (int i = 1; i < Images.Length; i++)
        {
            MagickImage image = Images[i];
            var matchedWidth = Math.Min(image.Width, baseImage.Width);
            var matchedHeight = Math.Min(image.Height, baseImage.Height);
            var channelMap = MapChannel(baseImage, image);
            List<PixelDiff> diff = [];

            var pixels = image.GetPixels();
            for (int x = 0; x < matchedWidth; x++)
            {
                for (int y = 0; y < matchedHeight; y++)
                {
                    var basePixel = basePixels[x, y];
                    var pixel = pixels[x, y];
                    float[] channelDiffs = new float[channelMap.Length];

                    if (basePixel == null || pixel == null)
                        diff.Add(new(x, y, channelDiffs));
                    else
                    {
                        for (uint c = 0; c < channelMap.Length; c++)
                            if (channelMap[c] == uint.MaxValue)
                                channelDiffs[c] = basePixel[c];
                            else
                                channelDiffs[c] = basePixel[c] - pixel[channelMap[c]];
                        if (channelDiffs.Any(d => d != 0))
                            diff.Add(new(x, y, channelDiffs));
                    }
                }
            }

            results[i - 1] = new() { DifferentPixels = [.. diff], Image = image, BaseImage = baseImage };
        }
        return results;
    }

    private static uint[] MapChannel(MagickImage baseImage, MagickImage image)
    {
        List<uint> map = [];
        PixelChannel[] baseChannels = [.. baseImage.Channels];
        PixelChannel[] channels = [.. image.Channels];
        for (uint i = 0; i < baseChannels.Length; i++)
        {
            uint r = uint.MaxValue;
            for (uint o = 0; o < channels.Length; o++)
                if (baseChannels[i] == channels[o]) { r = o; break; }
            map.Add(r);
        }
        return [.. map];
    }
}

public class MatchResult
{
    private MagickImage? diffImage;

    public bool IsExact => DifferentPixels.Length == 0;
    public required PixelDiff[] DifferentPixels { get; set; }
    public required MagickImage BaseImage { get; set; }
    public required MagickImage Image { get; set; }
    public MagickImage DiffImage => diffImage ??= GenerateDiffImage(DifferentPixels, Math.Min(Image.Width, BaseImage.Width), Math.Min(Image.Height, BaseImage.Height));

    private MagickImage GenerateDiffImage(PixelDiff[] diff, uint width, uint height)
    {
        var image = new MagickImage(MagickColors.Transparent, width, height)
        {
            ColorSpace = BaseImage.ColorSpace,
            Depth = BaseImage.Depth,
            HasAlpha = BaseImage.HasAlpha
        };
        var pixels = image.GetPixels();
        foreach (var pixel in diff)
            pixels.SetPixel(pixel.X, pixel.Y, [.. pixel.ChannelDiffs.Select(MathF.Abs)]);

        if (image.HasAlpha)
        {
            uint alphac = 0;
            for (alphac = 0; alphac < image.ChannelCount; alphac++)
                if (image.Channels.ElementAt((int)alphac) == PixelChannel.Alpha) break;

            Console.WriteLine(alphac);

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    var pixel = pixels.GetPixel(x, y);
                    var alpha = pixel.GetChannel(alphac);
                    pixel.SetChannel(alphac, Quantum.Max - alpha);
                }
            }
        }
        image.Transparent(MagickColors.Black);

        return image;
    }
}

public class PixelDiff(int x, int y, params float[] channelDiffs)
{
    public int X { get; } = x;
    public int Y { get; } = y;
    public float[] ChannelDiffs { get; set; } = channelDiffs;
}