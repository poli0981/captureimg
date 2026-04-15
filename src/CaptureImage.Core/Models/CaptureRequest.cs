namespace CaptureImage.Core.Models;

/// <summary>
/// Everything the capture pipeline needs to take one shot of one target.
/// </summary>
/// <param name="Target">The game / process / window the user is pointing at.</param>
/// <param name="Format">Encoding format for the saved file.</param>
/// <param name="OutputDirectory">Absolute path to the folder that will receive the file.</param>
/// <param name="FileNameTemplate">
/// Template for the output file name, without extension. Supported tokens (processed by
/// <c>FileNameStrategy</c>): <c>{Game}</c>, <c>{Process}</c>, <c>{yyyy}</c>, <c>{MM}</c>,
/// <c>{dd}</c>, <c>{HH}</c>, <c>{mm}</c>, <c>{ss}</c>, <c>{counter}</c>. Any other
/// <c>{...}</c> token is left untouched.
/// </param>
/// <param name="JpegQuality">1-100; ignored for non-JPEG formats.</param>
/// <param name="WebpQuality">1-100; ignored for non-WebP formats.</param>
public sealed record CaptureRequest(
    GameTarget Target,
    ImageFormat Format,
    string OutputDirectory,
    string FileNameTemplate,
    int JpegQuality = 90,
    int WebpQuality = 85);
