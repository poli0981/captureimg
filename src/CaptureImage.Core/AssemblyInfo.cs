using System.Runtime.CompilerServices;

// Expose internal helpers (e.g. FileNameStrategy.ExpandTokens / Sanitize) to the test assembly.
[assembly: InternalsVisibleTo("CaptureImage.Core.Tests")]

namespace CaptureImage.Core;

/// <summary>
/// Marker type for locating the <c>CaptureImage.Core</c> assembly from other projects
/// (e.g. for <see cref="System.Reflection.Assembly"/> lookups in DI or localization).
/// </summary>
public static class CoreAssemblyMarker
{
}
