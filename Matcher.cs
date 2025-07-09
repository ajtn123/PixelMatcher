using ImageMagick;

namespace PixelMatcher;

public class Matcher(params MagickImage[] images)
{
    public MagickImage[] Images { get; } = images;

    public MatchResult[] Match()
    {
        var results = new MatchResult[Images.Length - 1];

        if (Images.Any(i => i.HasAlpha))
            foreach (var image in Images)
                image.Alpha(AlphaOption.Set);

        var baseImage = Images[0];
        var basePixels = baseImage.GetPixels().ToArray();
        if (basePixels == null) return results;
        var baseWidth = baseImage.Width;
        var baseHeight = baseImage.Height;
        var baseChannelCount = baseImage.ChannelCount;

        for (int i = 1; i < Images.Length; i++)
        {
            MagickImage image = Images[i];
            var width = image.Width;
            var height = image.Height;
            var channelCount = image.ChannelCount;
            var matchedWidth = Math.Min(width, baseWidth);
            var matchedHeight = Math.Min(height, baseHeight);
            var channelMap = Utils.MapChannel(baseImage, image);
            (uint, uint)[] map = [.. channelMap.Select(x => (x.Item2, x.Item3))];
            List<PixelDiff> diff = [];

            var pixels = image.GetPixels().ToArray();
            if (pixels != null)
                for (int y = 0; y < matchedHeight; y++)
                {
                    var startingBaseIndex = y * baseWidth * baseChannelCount;
                    var startingIndex = y * width * channelCount;
                    for (int x = 0; x < matchedWidth; x++)
                    {
                        float[] channelDiffs = new float[map.Length];

                        for (uint c = 0; c < map.Length; c++)
                        {
                            var baseChannelIndex = map[c].Item1;
                            var channelIndex = map[c].Item2;
                            var baseChannel = baseChannelIndex == uint.MaxValue ? 0 : basePixels[startingBaseIndex + x * baseChannelCount + baseChannelIndex];
                            var channel = channelIndex == uint.MaxValue ? 0 : pixels[startingIndex + x * channelCount + channelIndex];
                            channelDiffs[c] = baseChannel - channel;
                        }

                        if (channelDiffs.Any(d => d != 0))
                            diff.Add(new(x, y, channelDiffs));
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
    private double? deviation = null;

    public bool IsExact => DifferentPixels.Length == 0;
    public double Deviation => deviation ??= DifferentPixels.MeanSquaredDeviation(MatchedWidth * MatchedHeight * ChannelMap.Length);
    public uint MatchedWidth => Math.Min(Image.Width, BaseImage.Width);
    public uint MatchedHeight => Math.Min(Image.Height, BaseImage.Height);
    public required (PixelChannel, uint, uint)[] ChannelMap { get; set; }
    public required PixelDiff[] DifferentPixels { get; set; }
    public required MagickImage BaseImage { get; set; }
    public required MagickImage Image { get; set; }
    public MagickImage DiffImage => diffImage ??= GenerateDiffImage();

    public MagickImage GenerateDiffImage()
    {
        var diff = DifferentPixels;
        var width = MatchedWidth;
        var height = MatchedHeight;
        var image = new MagickImage(MagickColors.Black, width, height)
        {
            ColorSpace = BaseImage.ColorSpace,
            Depth = BaseImage.Depth,
            HasAlpha = BaseImage.HasAlpha
        };
        var pixels = image.GetPixels();
        var pixelsArray = pixels.ToArray();
        if (pixelsArray == null) return image;
        var channelCount = (int)image.ChannelCount;
        var alphaChannelIndex = image.Channels.Index().FirstOrDefault(x => x.Item2 == PixelChannel.Alpha, (int.MinValue, PixelChannel.Alpha)).Item1;

        if (channelCount == BaseImage.ChannelCount)
            if (image.HasAlpha)
                foreach (var pixel in diff)
                {
                    var pixelData = pixel.ChannelDiffs.Select(MathF.Abs).ToArray();
                    pixelData[alphaChannelIndex] = Quantum.Max - pixelData[alphaChannelIndex];
                    Array.Copy(pixelData, 0, pixelsArray, (pixel.Y * width + pixel.X) * channelCount, channelCount);
                }
            else foreach (var pixel in diff)
                    Array.Copy(pixel.ChannelDiffs.Select(MathF.Abs).ToArray(), 0, pixelsArray, (pixel.Y * width + pixel.X) * channelCount, channelCount);
        else
        {
            Console.WriteLine("Remapping Diff Image Channel");
            var map = Utils.MapChannel(image, BaseImage).OrderBy(x => x.Item3).Select(x => x.Item2);

            if (image.HasAlpha)
                foreach (var pixel in diff)
                {
                    var pixelData = pixel.ChannelDiffs.Zip(map).OrderBy(x => x.Second).Take(channelCount).Select(x => MathF.Abs(x.First)).ToArray();
                    pixelData[alphaChannelIndex] = Quantum.Max - pixelData[alphaChannelIndex];
                    Array.Copy(pixelData, 0, pixelsArray, (pixel.Y * width + pixel.X) * channelCount, channelCount);
                }
            else foreach (var pixel in diff)
                    Array.Copy(pixel.ChannelDiffs.Zip(map).OrderBy(x => x.Second).Take(channelCount).Select(x => MathF.Abs(x.First)).ToArray(), 0, pixelsArray, (pixel.Y * width + pixel.X) * channelCount, channelCount);
        }

        pixels.SetPixels(pixelsArray);
        image.Transparent(MagickColors.Black);

        return diffImage = image;
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

        return [.. allChannels.Select(key => (key, baseMap.TryGetValue(key, out var v1) ? v1 : uint.MaxValue, map.TryGetValue(key, out var v2) ? v2 : uint.MaxValue)).OrderBy(x => x.Item2)];
    }

    public static double MeanSquaredDeviation(this PixelDiff[] diffs, long totalLength)
    {
        var deviation = 0d;
        foreach (var diff in diffs)
            foreach (var p in diff.ChannelDiffs)
                deviation += p * p;
        return deviation / totalLength;
    }

    public static IEnumerable<string> ShortenPaths(IEnumerable<string> paths)
    {
        if (!paths.Any()) return paths;

        var splitPaths = paths.Select(p => p.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).ToArray();

        int commonLength = Enumerable.Range(0, splitPaths.Min(p => p.Length))
            .TakeWhile(i => splitPaths.All(p => string.Equals(p[i], splitPaths[0][i], StringComparison.OrdinalIgnoreCase)))
            .Count();

        return splitPaths.Select(p => string.Join(Path.DirectorySeparatorChar, p.Skip(commonLength)));
    }

    public static void Log(object content) => Log(content, ConsoleColor.Gray);
    public static void Log(object content, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.Write(content);
    }
    public static void Logl(object content) => Logl(content, ConsoleColor.Gray);
    public static void Logl(object content, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.WriteLine(content);
    }
}
