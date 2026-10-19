// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) CaptureImage Contributors

using System.Text.Json;
using System.Text.Json.Serialization;
using CaptureImage.Core.Models;

namespace CaptureImage.Core.Tests.TestSupport;

/// <summary>
/// Reflection-based equivalent of the production settings serializer (the source-generated
/// context is internal to Infrastructure): camelCase, enums as strings, case-insensitive
/// names, comments and trailing commas accepted, nulls not written.
/// </summary>
internal static class SettingsJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static AppSettings Deserialize(string json) =>
        JsonSerializer.Deserialize<AppSettings>(json, Options)
            ?? throw new InvalidOperationException("The JSON document is null.");

    public static string Serialize(AppSettings settings) => JsonSerializer.Serialize(settings, Options);
}
