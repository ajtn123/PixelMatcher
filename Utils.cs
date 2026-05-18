using ImageMagick;

namespace PixelMatcher;

public static class Utils
{
    public static (PixelChannel, uint, uint)[] MapChannel(IEnumerable<PixelChannel> baseImage, IEnumerable<PixelChannel> image)
    {
        var baseMap = baseImage.Index().ToDictionary(t => t.Item, t => (uint)t.Index);
        var map = image.Index().ToDictionary(t => t.Item, t => (uint)t.Index);

        var allChannels = baseMap.Keys.Union(map.Keys);

        return [.. allChannels.Select(key => (key, baseMap.TryGetValue(key, out var v1) ? v1 : uint.MaxValue, map.TryGetValue(key, out var v2) ? v2 : uint.MaxValue)).OrderBy(x => x.Item2)];
    }

    public static double StandardDeviation(this PixelDiff[] pixelDiffs, long totalLength)
    {
        var deviation = 0d;
        foreach (var pixel in pixelDiffs)
            foreach (var channel in pixel.ChannelDiffs)
                deviation += channel * channel;
        return Math.Sqrt(deviation / totalLength);
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
        Console.ForegroundColor = ConsoleColor.Gray;
    }
}
