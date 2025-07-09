# Pixel Matcher

Compare images pixel by pixel, and produce graphs that show the differences.

## Usage

You can directly drop images on `PixelMatcher.exe` file, or you can use the following command.

```
PixelMatcher.exe <Path/To/Base/Image> <Path/To/Image> [<Path/To/Image1> [...]]
```

Diff-images are save in the working directory. If images are dropped, diff-images are save in the same directory as the base images.

Brightness of the channels in diff-images represents the difference between the respective channels of compared images. A darker pixel means less difference in that position, while a transparent pixel means no difference at all.

Note that comparsions between jpegs would often produce large areas of nearly black pixels because of jpeg's lossy compression algorithm, even for images exported form the same source.

For images will different dimensions, the top-left corner is compared.
