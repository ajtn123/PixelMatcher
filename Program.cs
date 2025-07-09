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

var imageNames = Utils.ShortenPaths(files.Select(x => x.FullName)).ToArray();
var baseImage = images[0];
Utils.Logl($"Base Image: {imageNames[0]}");
Utils.Logl($"Dimensions: {$"{baseImage.Width:n0}*{baseImage.Height:n0}",24}");

for (int i = 0; i < results.Length; i++)
{
    var r = results[i];
    var index = i + 1;
    var iWidth = r.Image.Width;
    var iHeight = r.Image.Height;
    var iPixels = iWidth * iHeight;
    var width = r.MatchedWidth;
    var height = r.MatchedHeight;
    var totalPixels = width * height;
    var matchedPercentage = (double)totalPixels / iPixels * 100;
    var diffPixels = r.DifferentPixels.Length;
    var diffPercentage = (double)diffPixels / totalPixels * 100;
    var deviation = r.Deviation;
    var maxDeviation = (double)Quantum.Max * Quantum.Max;
    var deviationPercentage = deviation / maxDeviation * 100;
    var isExact = r.IsExact;

    Utils.Logl($"{$"{index,2} Name",-24}: {imageNames[index]}");
    Utils.Logl($"{" | Compared Dimensions",-24}: {$"{width:n0}*{height:n0}",16} / {$"{iWidth:n0}*{iHeight:n0}",16} ({matchedPercentage,6:0.00}%)");
    Utils.Logl($"{" | Different Pixels",-24}: {diffPixels,16:n0} / {totalPixels,16:n0} ({diffPercentage,6:0.00}%)");
    Utils.Logl($"{" | Deviation",-24}: {deviation,16:n0} / {maxDeviation,16:n0} ({deviationPercentage,6:0.00}%)");
    Utils.Log($"{" | Is Exact",-24}: "); Utils.Logl($"{isExact,35}", isExact ? ConsoleColor.Green : ConsoleColor.Red);
}

stopwatch.Stop();
Utils.Logl($"Time Used: {stopwatch.Elapsed.TotalMilliseconds} ms");

Utils.Logl("Save Diff Images? (Y/n)");
if (Console.ReadLine()?.Any("Nn".Contains) ?? false) return;

var formats = MagickNET.SupportedFormats.Where(x => x.SupportsWriting).Select(x => x.Format.ToString().ToLower());
Utils.Logl($"Format? ({formats.Aggregate((x, y) => x + "/" + y).Replace("/png/", "/").Insert(0, "PNG/")})");
var format = Console.ReadLine()?.Trim('.').ToLower();
if (!formats.Contains(format)) format = "png";

Utils.Logl("Quality? (75/0-100)");
bool isQualitySet = uint.TryParse(Console.ReadLine(), out uint quality);

var opID = DateTime.Now.Millisecond;
for (int i = 0; i < results.Length; i++)
{
    var diffImage = await diffImagesTasks[i];
    if (isQualitySet) diffImage.Quality = Math.Clamp(quality, 0, 100);
    Utils.Logl($"Writing 'diff-{opID,3:000}-{i}.{format}'{(isQualitySet ? $" with quality of {diffImage.Quality}" : "")}");
    diffImage.Write($"diff-{opID,3:000}-{i}.{format}");
}
