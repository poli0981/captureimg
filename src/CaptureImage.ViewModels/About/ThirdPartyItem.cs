namespace CaptureImage.ViewModels.About;

/// <summary>
/// Single row in the About tab's third-party attribution list. Rendered by an
/// <c>ItemsControl</c> in <c>AboutView.axaml</c>.
/// </summary>
/// <param name="Name">Package or component name (e.g. <c>Avalonia</c>).</param>
/// <param name="License">SPDX license identifier (e.g. <c>MIT</c>, <c>Apache-2.0</c>).</param>
/// <param name="Url">Upstream project URL. May be empty if not public.</param>
public sealed record ThirdPartyItem(string Name, string License, string Url);
