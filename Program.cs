using System.Data.Common;
using System.Diagnostics;
using ImageMagick;
using PixelMatcher;

FileInfo[] files = [.. args.Select(x => new FileInfo(x)).Where(x => x.Exists)];

if (files.Length < 2) return;

MagickImage[] images = [.. files.Select(x => new MagickImage(x))];

if (images.Length < 2) return;

var matcher = new Matcher(images);

Stopwatch stopwatch = Stopwatch.StartNew();

var results = matcher.Match();

for (int i = 0; i < results.Length; i++)
    Console.WriteLine($"{i + 1}. Different Pixel: {results[i].DifferentPixels.Length}");

var diffImages = results.Select(r => r.DiffImage).ToArray();

stopwatch.Stop();
Console.WriteLine($"Time Used: {stopwatch.Elapsed.TotalMilliseconds} ms");

Console.WriteLine("Save Diff Images? (Y/n)");
if (Console.ReadLine()?.Contains('n') ?? false) return;

var formats = MagickNET.SupportedFormats.Where(x => x.SupportsWriting).Select(x => x.Format.ToString().ToLower());
Console.WriteLine($"Format? ({formats.Aggregate((x, y) => x + "/" + y).Replace("/png/", "/").Insert(0, "PNG/")})");
var format = Console.ReadLine()?.Trim('.').ToLower();
if (!formats.Contains(format)) format = "png";

Console.WriteLine("Quality? (75/0-100)");
bool isQualitySet = uint.TryParse(Console.ReadLine(), out uint quality);

var opID = DateTime.Now.Millisecond;
for (int i = 0; i < results.Length; i++)
{
    if (isQualitySet) diffImages[i].Quality = Math.Clamp(quality, 0, 100);
    Console.WriteLine($"Writing 'diff-{opID,3:000}-{i}.{format}' with quality of {diffImages[i].Quality}");
    diffImages[i].Write($"diff-{opID,3:000}-{i}.{format}");
}
