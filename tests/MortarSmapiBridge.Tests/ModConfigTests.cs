using System.Text.Json;
using Xunit;

namespace MortarSmapiBridge.Tests;

public class ModConfigTests
{
    // The overlay moved to its own mod, which reads startup/overlay.json; the bridge's config must not keep keys it no
    // longer honours.
    [Fact]
    public void ConfigHasNoOverlayKeys()
    {
        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(new ModConfig(), new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        Assert.DoesNotContain(
            json.RootElement.EnumerateObject(),
            property => property.Name.Contains("overlay", StringComparison.OrdinalIgnoreCase));
    }
}
