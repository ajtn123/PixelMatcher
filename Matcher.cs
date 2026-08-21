using ImageMagick;

namespace PixelMatcher;

public static class Matcher
{
    public static ParallelQuery<MatchResult> Match(FileInfo baseImageFile, IEnumerable<FileInfo> imageFiles, ResizeMode resizeMode = ResizeMode.None)
    {
        var baseImage = new MagickImage(baseImageFile);
        var baseInfo = ImageInfo.From(baseImage);

        var basePixels = baseImage.GetPixelsUnsafe().ToArray() ?? throw new Exception($"Cannot read {baseImageFile.FullName}");
        baseImage.Dispose();

        return imageFiles.AsParallel().Select(imageFile =>
        {
            var image = new MagickImage(imageFile);
            Resizer.Resize(image, baseInfo.Width, baseInfo.Height, resizeMode);
            var info = ImageInfo.From(image);

            var matchedWidth = Math.Min(baseInfo.Width, info.Width);
            var matchedHeight = Math.Min(baseInfo.Height, info.Height);
            var channelMap = Utils.MapChannel(baseInfo.Channels, info.Channels).ToArray();

            var pixels = image.GetPixelsUnsafe().ToArray() ?? throw new Exception($"Cannot read {imageFile.FullName}");
            image.Dispose();

            var diff = new List<PixelDiff>(1 << 16);
            for (uint y = 0; y < matchedHeight; y++)
            {
                var startingBaseIndex = y * baseInfo.Width * baseInfo.ChannelCount;
                var startingIndex = y * info.Width * info.ChannelCount;
                for (uint x = 0; x < matchedWidth; x++)
                {
                    var channelDiffs = new byte[channelMap.Length];

                    for (uint c = 0; c < channelMap.Length; c++)
                    {
                        var baseChannel = basePixels[startingBaseIndex + x * baseInfo.ChannelCount + channelMap[c].BaseIndex];
                        var channel = pixels[startingIndex + x * info.ChannelCount + channelMap[c].Index];
                        channelDiffs[c] = (byte)Math.Abs(baseChannel - channel);
                    }

                    if (channelDiffs.Any(d => d != 0))
                        diff.Add(new(x, y, channelDiffs));
                }
            }

            return new MatchResult(baseImageFile, baseInfo, imageFile, info, matchedWidth, matchedHeight, channelMap, diff);
        });
    }
}

public record MatchResult(
    FileInfo BaseImageFile, ImageInfo BaseImageInfo,
    FileInfo ImageFile, ImageInfo ImageInfo,
    uint MatchedWidth, uint MatchedHeight,
    IEnumerable<Utils.ChannelPosition> ChannelMap,
    List<PixelDiff> DifferentPixels
)
{
    public int Index { get; set; }

    public bool Identical => DifferentPixels.Count == 0;

    public double StandardDeviation()
    {
        long deviation = 0;
        foreach (var pixel in DifferentPixels)
            foreach (var channel in pixel.ChannelDiffs)
                deviation += channel * channel;
        return Math.Sqrt(deviation / (MatchedWidth * MatchedHeight * ChannelMap.Count()));
    }

    public IEnumerable<(PixelChannel Channel, bool IsFromBase)> UncomparedChannels =>
    [
        .. BaseImageInfo.Channels.Except(ChannelMap.Select(o => o.Channel)).Select(o => (o, true)),
        .. ImageInfo.Channels.Except(ChannelMap.Select(o => o.Channel)).Select(o => (o, false)),
    ];

    public MagickImage GenerateDiffImage()
    {
        var diff = DifferentPixels;
        var width = MatchedWidth;
        var height = MatchedHeight;
        var hasAlpha = ChannelMap.Any(o => o.Channel is PixelChannel.Alpha);
        var image = new MagickImage(MagickColors.Transparent, width, height)
        {
            ColorSpace = new MagickImageInfo(BaseImageFile).ColorSpace,
            HasAlpha = true
        };
        var aplhaChannelIndex = image.Channels.Index().First(o => o.Item is PixelChannel.Alpha).Index;
        var channelCount = image.ChannelCount;
        var channelMap = Utils.MapChannel(image.Channels, ChannelMap.Select(x => x.Channel)).ToArray();

        var pc = image.GetPixelsUnsafe();
        var pixels = pc.ToArray() ?? throw new Exception($"Cannot generate diff image between {BaseImageFile.FullName} and {ImageFile.FullName}");

        foreach (var pixel in diff)
        {
            var startingIndex = (pixel.Y * width + pixel.X) * channelCount;
            for (uint c = 0; c < channelMap.Length; c++)
            {
                pixels[startingIndex + channelMap[c].BaseIndex] = pixel.ChannelDiffs[channelMap[c].Index];
            }
            pixels[startingIndex + aplhaChannelIndex] = (byte)(Quantum.Max - pixels[startingIndex + aplhaChannelIndex]);
        }

        pc.SetPixels(pixels);

        return image;
    }
}

public readonly record struct PixelDiff(uint X, uint Y, byte[] ChannelDiffs);

public readonly record struct ImageInfo(uint ChannelCount, uint Width, uint Height, PixelChannel[] Channels)
{
    public static ImageInfo From(MagickImage image) => new(image.ChannelCount, image.Width, image.Height, [.. image.Channels]);
}
