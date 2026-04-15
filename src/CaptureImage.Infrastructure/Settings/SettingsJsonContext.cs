using System.Text.Json.Serialization;
using CaptureImage.Core.Models;

namespace CaptureImage.Infrastructure.Settings;

/// <summary>
/// <see cref="JsonSerializerContext"/> for source-generated (AOT-friendly) serialization of
/// <see cref="AppSettings"/>. Keeps reflection off the hot path and lights up ahead-of-time
/// compilation when we flip the switch.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(CaptureSettings))]
[JsonSerializable(typeof(UiSettings))]
[JsonSerializable(typeof(HotkeyBinding))]
[JsonSerializable(typeof(HotkeyModifiers))]
[JsonSerializable(typeof(ImageFormat))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext
{
}
