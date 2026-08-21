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

    private static void W(object content, ConsoleColor? color = null)
    {
        if (color != null)
            Console.ForegroundColor = color.Value;
        Console.Write(content);
        Console.ResetColor();
    }

    private static void E() => Console.WriteLine();

    public static void Pad(object first, object second) => W(new string(' ', Math.Max(2, 64 - $"{first}{second}".Length)));

    public static void WriteTitle(object icon, object title, ConsoleColor? color)
    {
        W($"{icon,2} {title}", color); E();
    }

    public static void WriteInfo(object key, object value, ConsoleColor? color = null)
    {
        W(" | "); W(key); Pad(key, value); W(value, color); E();
    }

    public static void WriteInfoProportion(object key, object value, object maximum, object proportion)
    {
        var fraction = $"{value:n0} / {maximum,16:n0}";
        W(" | "); W(key); Pad(key, fraction); W(fraction); W($"{proportion,8:P}"); E();
    }
}
