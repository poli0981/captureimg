// Marker file to ensure the Core assembly builds.
// Real types will land in Abstractions/, Models/, StateMachine/, Pipeline/, Errors/ as M1+ progresses.

namespace CaptureImage.Core;

/// <summary>
/// Marker type for locating the <c>CaptureImage.Core</c> assembly from other projects
/// (e.g. for <see cref="System.Reflection.Assembly"/> lookups in DI or localization).
/// </summary>
public static class CoreAssemblyMarker
{
}
