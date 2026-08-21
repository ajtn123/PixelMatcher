using ImageMagick;

namespace PixelMatcher;

public static class Utils
{
    public readonly record struct ChannelPosition(PixelChannel Channel, uint BaseIndex, uint Index);

    public static IEnumerable<ChannelPosition> MapChannel(IEnumerable<PixelChannel> baseChannels, IEnumerable<PixelChannel> channels)
    {
        var sharedChannels = baseChannels.Intersect(channels);
        var baseMap = baseChannels.Index().ToDictionary(o => o.Item, o => (uint)o.Index);
        var map = channels.Index().ToDictionary(o => o.Item, o => (uint)o.Index);

        return sharedChannels.Select(c => new ChannelPosition(c, baseMap[c], map[c]));
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

    private static void L(object content, ConsoleColor? color = null)
    {
        if (color != null)
            Console.ForegroundColor = color.Value;
        Console.Write(content);
        Console.ResetColor();
    }

    private static void Ll(object content, ConsoleColor? color = null)
    {
        L(content, color);
        Console.WriteLine();
    }

    public static void WriteTitle(object icon, object title, ConsoleColor? color)
    {
        Ll($"{icon,2} {title}", color);
    }

    public static void WriteInfo(object key, object value, ConsoleColor? color = null)
    {
        L($" | {key,-24} "); Ll($"{value,35}", color);
    }

    public static void WriteInfoProportion(object key, object value, object maximum, object proportion)
    {
        Ll($" | {key,-24} {value,16:n0} / {maximum,16:n0} {proportion,8:P}");
    }
}
