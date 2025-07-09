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
var diffImagesTasks = results.Select(r => Task.Run(r.GenerateDiffImage)).ToArray();

for (int i = 0; i < results.Length; i++)
    Console.WriteLine($"{$"{i + 1,2}. Different Pixels:",-22}{results[i].DifferentPixels.Length,16:n0}\n{" |  Deviation:",-22}{results[i].Deviation,16:n0}");

stopwatch.Stop();
Console.WriteLine($"Time Used: {stopwatch.Elapsed.TotalMilliseconds} ms");

Console.WriteLine("Save Diff Images? (Y/n)");
if (Console.ReadLine()?.Any("Nn".Contains) ?? false) return;

var formats = MagickNET.SupportedFormats.Where(x => x.SupportsWriting).Select(x => x.Format.ToString().ToLower());
Console.WriteLine($"Format? ({formats.Aggregate((x, y) => x + "/" + y).Replace("/png/", "/").Insert(0, "PNG/")})");
var format = Console.ReadLine()?.Trim('.').ToLower();
if (!formats.Contains(format)) format = "png";

Console.WriteLine("Quality? (75/0-100)");
bool isQualitySet = uint.TryParse(Console.ReadLine(), out uint quality);

var opID = DateTime.Now.Millisecond;
for (int i = 0; i < results.Length; i++)
{
    var diffImage = await diffImagesTasks[i];
    if (isQualitySet) diffImage.Quality = Math.Clamp(quality, 0, 100);
    Console.WriteLine($"Writing 'diff-{opID,3:000}-{i}.{format}'{(isQualitySet ? $" with quality of {diffImage.Quality}" : "")}");
    diffImage.Write($"diff-{opID,3:000}-{i}.{format}");
}
