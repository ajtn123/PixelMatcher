using ImageMagick;
using PixelMatcher;

FileInfo[] files = [.. args.Select(x => new FileInfo(x)).Where(x => x.Exists)];

if (files.Length < 2) return;

MagickImage[] images = [.. files.Select(x => new MagickImage(x))];

if (images.Length < 2) return;

var matcher = new Matcher(images);

var results = matcher.Match();

// foreach (var result in results)
//     foreach (var p in result.DifferentPixels)
//         Console.WriteLine($"{p.X,6}{p.Y,6}:{p.ChannelDiffs.Aggregate("", (a, b) => $"{a}{b,10:0.00}")}");

foreach (var result in results)
    Console.WriteLine($"Different Pixel: {result.DifferentPixels.Length,12}");

for (int i = 0; i < results.Length; i++)
{
    MagickImage? image = results[i].DiffImage;
    image.Write($"diff-{i}.bmp");
}

