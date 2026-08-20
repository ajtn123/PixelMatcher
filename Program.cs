using System.Diagnostics;
using ImageMagick;
using PixelMatcher;

var files = args
    .Where(x => !string.IsNullOrWhiteSpace(x))
    .Select(x => new FileInfo(x))
    .ToArray();

var missingFiles = files.Where(f => !f.Exists);
if (missingFiles.Any())
{
    Utils.WriteTitle("!", "Missing Files", ConsoleColor.Red);
    foreach (var missingFile in missingFiles)
        Utils.WriteTitle("|", missingFile.FullName, ConsoleColor.Red);
    return;
}

Stopwatch stopwatch = Stopwatch.StartNew();

var results = Matcher.Match(files.First(), files.Skip(1)).ToArray();

stopwatch.Stop();

var fileNames = Utils.ShortenPaths(files.Select(x => x.FullName)).ToArray();
Utils.WriteTitle(0, fileNames[0], ConsoleColor.Yellow);
Utils.WriteInfo("Dimensions", $"{results[0].BaseImageInfo.Width:n0}*{results[0].BaseImageInfo.Height:n0}");

for (int i = 1; i < files.Length; i++)
{
    var r = results.First(o => o.ImageFile == files[i]);
    r.Index = i;
    var imageWidth = r.ImageInfo.Width;
    var imageHeight = r.ImageInfo.Height;
    var imagePixels = imageWidth * imageHeight;
    var matchedWidth = r.MatchedWidth;
    var matchedHeight = r.MatchedHeight;
    var matchedPixels = matchedWidth * matchedHeight;
    var matchedPercentage = (double)matchedPixels / imagePixels;
    var diffPixels = r.DifferentPixels.Count;
    var diffPercentage = (double)diffPixels / matchedPixels;
    var deviation = r.StandardDeviation();
    var maxDeviation = Quantum.Max;
    var deviationPercentage = deviation / maxDeviation;
    var uncompared = r.UncomparedChannels;
    var identical = r.Identical;

    Utils.WriteTitle(i, fileNames[i], ConsoleColor.Blue);
    Utils.WriteInfoProportion("Compared Area", $"{matchedWidth:n0}*{matchedHeight:n0}", $"{imageWidth:n0}*{imageHeight:n0}", matchedPercentage);
    Utils.WriteInfoProportion("Different Pixels", diffPixels, matchedPixels, diffPercentage);
    Utils.WriteInfoProportion("Standard Deviation", $"{deviation:n2}", maxDeviation, deviationPercentage);
    foreach ((var channel, var isFromBase) in uncompared)
    {
        Utils.WriteInfo("Uncompared Channel", channel, isFromBase ? ConsoleColor.Yellow : ConsoleColor.Blue);
    }
    Utils.WriteInfo("Identical", identical, identical ? ConsoleColor.Green : ConsoleColor.Red);
}

var identicalCount = results.Count(x => x.Identical);
var identicalPercentage = identicalCount / results.Length;

Utils.WriteTitle("+", "Summary", ConsoleColor.Yellow);
Utils.WriteInfo("Time Used", $"{stopwatch.Elapsed.TotalSeconds} s");
Utils.WriteInfoProportion("Identical Images", identicalCount, results.Length, identicalPercentage);

if (identicalCount == results.Length)
{
    Utils.WriteTitle("!", "All Images Are Identical", ConsoleColor.Green);
    return;
}

Utils.WriteTitle("+", "Diff Images", ConsoleColor.Yellow);

if (Utils.Read("Save", "Y", "n").ToUpper() is not "Y") return;

var formats = MagickNET.SupportedFormats
    .Where(x => x.SupportsWriting)
    .Select(x => x.Format.ToString().ToLower());
if (Utils.Read("Format", "PNG").Trim('.').ToLower() is not { } format || !formats.Contains(format))
{
    Utils.WriteTitle("!", "Format is not supported", ConsoleColor.Red);
    return;
}

var opid = Random.Shared.GetHexString(4, true);
foreach (var r in results.Where(r => !r.Identical))
{
    var diffImage = r.GenerateDiffImage();
    var diffImageFile = $"diff-{opid}-{r.Index}.{format}";
    Utils.WriteTitle("|", $"Writing {diffImageFile}", ConsoleColor.Yellow);
    diffImage.Write(diffImageFile);
    diffImage.Dispose();
}
