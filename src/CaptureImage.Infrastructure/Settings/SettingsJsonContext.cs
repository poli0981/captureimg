// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Text.Json.Serialization;
using CaptureImage.Core.Library;
using CaptureImage.Core.Models;

namespace CaptureImage.Infrastructure.Settings;

/// <summary>
/// Source-generated System.Text.Json metadata for settings and the library index (no
/// reflection at runtime). Comments and trailing commas are accepted so a hand-edited
/// <c>settings.json</c> still loads.
/// </summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true,
    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(List<CaptureRecord>))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext
{
}
