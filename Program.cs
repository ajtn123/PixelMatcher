using System.CommandLine;
using System.Diagnostics;
using ImageMagick;
using PixelMatcher;

var baseImageArgument = new Argument<FileInfo>("base-image")
{
    Description = "Image to compare against.",
}.AcceptExistingOnly();

var imagesArgument = new Argument<FileInfo[]>("images")
{
    Description = "Images to compare with the base image.",
    Arity = ArgumentArity.OneOrMore,
}.AcceptExistingOnly();

var resizeOption = new Option<ResizeMode>("--resize", "-r")
{
    Description = "Resize comparison images before comparing.",
    DefaultValueFactory = _ => ResizeMode.None,
};

var diffImageOption = new Option<bool>("--diff-image")
{
    Description = "Write diff images.",
    DefaultValueFactory = _ => false,
};

var diffImageFormatOption = new Option<MagickFormat>("--diff-image-format")
{
    Description = "Format of diff images.",
    DefaultValueFactory = _ => MagickFormat.Png,
    HelpName = "image-format",
};

var diffImageQualityOption = new Option<uint>("--diff-image-quality")
{
    Description = "Quality of diff images.",
    DefaultValueFactory = _ => 100,
    HelpName = "0-100",
};

var rootCommand = new RootCommand("Compare images pixel by pixel.")
{
    baseImageArgument,
    imagesArgument,
    resizeOption,
    diffImageOption,
    diffImageFormatOption,
    diffImageQualityOption,
};

rootCommand.SetAction(parseResult =>
{
    var baseImage = parseResult.GetRequiredValue(baseImageArgument);
    var images = parseResult.GetRequiredValue(imagesArgument);
    var resize = parseResult.GetValue(resizeOption);
    var diffImage = parseResult.GetValue(diffImageOption);
    var diffFormat = parseResult.GetValue(diffImageFormatOption);
    var diffQuality = parseResult.GetValue(diffImageQualityOption);

    var files = (FileInfo[])[baseImage, .. images];
    var fileNames = Utils.ShortenPaths(files.Select(x => x.FullName)).ToArray();

    var formats = MagickNET.SupportedFormats
        .Where(x => x.SupportsWriting)
        .Select(x => x.Format);
    if (!formats.Contains(diffFormat))
    {
        Utils.WriteTitle("!", $"{diffFormat} is not supported for writing", ConsoleColor.Red);
        return 1;
    }

    Stopwatch stopwatch = Stopwatch.StartNew();
    var results = Matcher.Match(baseImage, images, resize).ToArray();
    stopwatch.Stop();

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
        Utils.WriteInfoProportion("Standard Deviation", $"{deviation:n3}", maxDeviation, deviationPercentage);
        foreach ((var channel, var isFromBase) in uncompared)
        {
            Utils.WriteInfo("Uncompared Channel", channel, isFromBase ? ConsoleColor.Yellow : ConsoleColor.Blue);
        }
        Utils.WriteInfo("Identical", identical, identical ? ConsoleColor.Green : ConsoleColor.Red);
    }

    var identicalCount = results.Count(x => x.Identical);
    var identicalPercentage = identicalCount / results.Length;

    Utils.WriteTitle("+", "Summary", ConsoleColor.Yellow);
    Utils.WriteInfo("Time Used", $"{stopwatch.Elapsed.TotalSeconds:n3} s");
    Utils.WriteInfoProportion("Identical Images", identicalCount, results.Length, identicalPercentage);

    if (identicalCount == results.Length)
    {
        Utils.WriteTitle("!", "All Images Are Identical", ConsoleColor.Green);
        return 0;
    }

    if (!diffImage)
        return 0;

    Utils.WriteTitle("+", "Diff Images", ConsoleColor.Yellow);

    var opid = Random.Shared.GetHexString(4, true);
    foreach (var r in results.Where(r => !r.Identical))
    {
        var diffImageMagick = r.GenerateDiffImage();
        diffImageMagick.Format = diffFormat;
        diffImageMagick.Quality = diffQuality;
        var diffImageFile = $"diff-{opid}-{r.Index}.{diffFormat.ToString().ToLower()}";
        Utils.WriteTitle("|", $"Writing {diffImageFile}", ConsoleColor.Yellow);
        diffImageMagick.Write(diffImageFile);
        diffImageMagick.Dispose();
    }

    return 0;
});

return rootCommand.Parse(args).Invoke();
