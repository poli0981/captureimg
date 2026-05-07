using Xunit;

namespace CaptureImage.UI.Tests.Localization;

/// <summary>
/// xUnit collection that serialises every test class touching
/// <see cref="System.Globalization.CultureInfo.DefaultThreadCurrentUICulture"/> /
/// <c>DefaultThreadCurrentCulture</c>. Those properties are process-wide statics, so
/// without this guard a Theory iteration in one class can stomp on a static
/// assertion in another class running on a parallel runner thread.
/// </summary>
[CollectionDefinition(Name)]
public sealed class LocalizationTestCollection
{
    public const string Name = "Localization (serialised — touches static CultureInfo)";
}
