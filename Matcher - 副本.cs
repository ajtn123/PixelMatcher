using ImageMagick;

namespace PixelMatcher1;

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
            var channelMap = Utils.MapChannel(baseImage, image);
            (uint, uint)[] map = [.. channelMap.Select(x => (x.Item2, x.Item3))];
            List<PixelDiff> diff = [];

            var pixels = image.GetPixels();
            for (int x = 0; x < matchedWidth; x++)
            {
                for (int y = 0; y < matchedHeight; y++)
                {
                    var basePixel = basePixels[x, y];
                    var pixel = pixels[x, y];
                    float[] channelDiffs = new float[map.Length];

                    if (basePixel == null || pixel == null)
                        diff.Add(new(x, y, channelDiffs));
                    else
                    {
                        for (uint c = 0; c < map.Length; c++)
                        {
                            var baseChannelIndex = map[c].Item1;
                            var channelIndex = map[c].Item2;
                            var baseChannel = baseChannelIndex == uint.MaxValue ? 0
                                        : baseChannelIndex == uint.MaxValue - 1 ? Quantum.Max
                                                                                : basePixel[baseChannelIndex];
                            var channel = channelIndex == uint.MaxValue ? 0
                                    : channelIndex == uint.MaxValue - 1 ? Quantum.Max
                                                                        : pixel[channelIndex];
                            channelDiffs[c] = baseChannel - channel;
                        }
                        if (channelDiffs.Any(d => d != 0))
                            diff.Add(new(x, y, channelDiffs));
                    }
                }
            }

            results[i - 1] = new() { DifferentPixels = [.. diff], Image = image, BaseImage = baseImage, ChannelMap = channelMap };
        }
        return results;
    }
}

public class MatchResult
{
    private MagickImage? diffImage;

    public bool IsExact => DifferentPixels.Length == 0;
    public required (PixelChannel, uint, uint)[] ChannelMap { get; set; }
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
        if (image.ChannelCount == BaseImage.ChannelCount)
            foreach (var pixel in diff)
                pixels.SetPixel(pixel.X, pixel.Y, [.. pixel.ChannelDiffs.Take((int)image.ChannelCount).Select(MathF.Abs)]);
        else
        {
            Console.WriteLine("Remapping Diff Image Channel");
            var map = Utils.MapChannel(image, BaseImage).OrderBy(x => x.Item3).Select(x => x.Item2);
            foreach (var pixel in diff)
                pixels.SetPixel(pixel.X, pixel.Y, [.. pixel.ChannelDiffs.Zip(map).OrderBy(x => x.Second).Take((int)image.ChannelCount).Select(x => MathF.Abs(x.First))]);
        }

        if (image.HasAlpha)
        {
            uint alphac;
            for (alphac = 0; alphac < image.ChannelCount; alphac++)
                if (image.Channels.ElementAt((int)alphac) == PixelChannel.Alpha) break;

            Console.WriteLine($"Inverting Alpha Channel: {alphac}");

            for (int y = 0; y < image.Height; y++)
                for (int x = 0; x < image.Width; x++)
                {
                    var pixel = pixels.GetPixel(x, y);
                    var alpha = pixel.GetChannel(alphac);
                    pixel.SetChannel(alphac, Quantum.Max - alpha);
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

public static class Utils
{
    public static (PixelChannel, uint, uint)[] MapChannel(MagickImage baseImage, MagickImage image)
    {
        var baseMap = baseImage.Channels.Index().ToDictionary(t => t.Item, t => (uint)t.Index);
        var map = image.Channels.Index().ToDictionary(t => t.Item, t => (uint)t.Index);

        var allChannels = baseMap.Keys.Union(map.Keys);

        return [.. allChannels.Select(key => (key, baseMap.TryGetValue(key, out var v1) ? v1 : key == PixelChannel.Alpha ? uint.MaxValue - 1 : uint.MaxValue, map.TryGetValue(key, out var v2) ? v2 : key == PixelChannel.Alpha ? uint.MaxValue - 1 : uint.MaxValue)).OrderBy(x => x.Item2)];
    }
}