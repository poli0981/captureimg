namespace CaptureImage.ViewModels.About;

/// <summary>
/// Single entry in the About tab's "Connect with the developer" list. Rendered
/// by an <c>ItemsControl</c> in <c>AboutPage.xaml</c>; the <c>HyperlinkButton</c>
/// item template binds <see cref="NavigateUri"/> so WinUI 3 opens the URL via
/// the system shell handler on click — same behaviour as the OpenUrl helper.
/// </summary>
/// <param name="Label">User-visible label, e.g. <c>X (Twitter) · @SkullMute0011</c>. Not localized — handles are universal.</param>
/// <param name="NavigateUri">Absolute URL to the maintainer's public profile on the platform.</param>
public sealed record SocialLink(string Label, Uri NavigateUri);
