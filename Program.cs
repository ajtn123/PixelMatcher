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

var opID = DateTime.Now.Millisecond;
for (int i = 0; i < results.Length; i++)
    diffImages[i].Write($"diff-{opID,3:000}-{i}.png");
