using ImageMagick;

namespace PixelMatcher;

public static class Matcher
{
    public static MatchResult[] Match(FileInfo baseImageFile, params FileInfo[] imageFiles)
    {
        var results = new MatchResult[imageFiles.Length];

        using MagickImage baseImage = new(baseImageFile);
        BaseImageInfo baseInfo = new(baseImage);
        var basePixels = baseImage.GetPixels().ToArray();
        if (basePixels == null) return results;

        for (int i = 0; i < imageFiles.Length; i++)
        {
            using MagickImage image = new(imageFiles[i]);
            if (baseImage.HasAlpha) image.Alpha(AlphaOption.Set);
            else if (image.HasAlpha) baseImage.Alpha(AlphaOption.Activate);
            var width = image.Width;
            var height = image.Height;
            var channelCount = image.ChannelCount;
            var matchedWidth = Math.Min(width, baseInfo.Width);
            var matchedHeight = Math.Min(height, baseInfo.Height);
            var channelMap = Utils.MapChannel(baseImage.Channels, image.Channels);
            (uint, uint)[] map = [.. channelMap.Select(x => (x.Item2, x.Item3))];
            List<PixelDiff> diff = [];

            var pixels = image.GetPixelsUnsafe().ToArray();
            if (pixels != null)
                for (int y = 0; y < matchedHeight; y++)
                {
                    var startingBaseIndex = y * baseInfo.Width * baseInfo.ChannelCount;
                    var startingIndex = y * width * channelCount;
                    for (int x = 0; x < matchedWidth; x++)
                    {
                        float[] channelDiffs = new float[map.Length];

                        for (uint c = 0; c < map.Length; c++)
                        {
                            var baseChannelIndex = map[c].Item1;
                            var channelIndex = map[c].Item2;
                            var baseChannel = baseChannelIndex == uint.MaxValue ? 0 : basePixels[startingBaseIndex + x * baseInfo.ChannelCount + baseChannelIndex];
                            var channel = channelIndex == uint.MaxValue ? 0 : pixels[startingIndex + x * channelCount + channelIndex];
                            channelDiffs[c] = baseChannel - channel;
                        }

                        if (channelDiffs.Any(d => d != 0))
                            diff.Add(new(x, y, channelDiffs));
                    }
                }
            if (baseImage.HasAlpha && !baseInfo.HasAlpha) baseImage.Alpha(AlphaOption.Deactivate);
            results[i] = new() { DifferentPixels = [.. diff], ImageWidth = image.Width, ImageHeight = image.Height, BaseImageInfo = baseInfo, ChannelMap = channelMap };
        }
        return results;
    }
}

public class MatchResult
{
    private double? deviation = null;

    public bool IsExact => DifferentPixels.Length == 0;
    public double Deviation => deviation ??= DifferentPixels.MeanSquaredDeviation(MatchedWidth * MatchedHeight * ChannelMap.Length);
    public required uint ImageWidth { get; set; }
    public required uint ImageHeight { get; set; }
    public uint MatchedWidth => Math.Min(ImageWidth, BaseImageInfo.Width);
    public uint MatchedHeight => Math.Min(ImageHeight, BaseImageInfo.Height);
    public required (PixelChannel, uint, uint)[] ChannelMap { get; set; }
    public required PixelDiff[] DifferentPixels { get; set; }
    public required BaseImageInfo BaseImageInfo { get; set; }

    public MagickImage GenerateDiffImage()
    {
        var diff = DifferentPixels;
        var width = MatchedWidth;
        var height = MatchedHeight;
        var image = new MagickImage(MagickColors.Black, width, height)
        {
            ColorSpace = BaseImageInfo.ColorSpace,
            Depth = BaseImageInfo.Depth,
            HasAlpha = BaseImageInfo.HasAlpha
        };
        var pixels = image.GetPixels();
        var pixelsArray = pixels.ToArray();
        if (pixelsArray == null) return image;
        var channelCount = (int)image.ChannelCount;
        var alphaChannelIndex = image.Channels.Index().FirstOrDefault(x => x.Item2 == PixelChannel.Alpha, (int.MinValue, PixelChannel.Alpha)).Item1;

        if (channelCount == BaseImageInfo.ChannelCount)
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
            var map = Utils.MapChannel(image.Channels, BaseImageInfo.Channels).OrderBy(x => x.Item3).Select(x => x.Item2);

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

        return image;
    }
}

public readonly struct PixelDiff(int x, int y, params float[] channelDiffs)
{
    public int X { get; } = x;
    public int Y { get; } = y;
    public float[] ChannelDiffs { get; } = channelDiffs;
}

public class BaseImageInfo(MagickImage image)
{
    public uint Width { get; } = image.Width;
    public uint Height { get; } = image.Height;
    public PixelChannel[] Channels { get; } = [.. image.Channels];
    public uint ChannelCount { get; } = image.ChannelCount;
    public ColorSpace ColorSpace { get; } = image.ColorSpace;
    public uint Depth { get; } = image.Depth;
    public bool HasAlpha { get; } = image.HasAlpha;
}
