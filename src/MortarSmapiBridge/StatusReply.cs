using System.Text.Json;

namespace MortarSmapiBridge;

/// <summary>The <c>status</c> reply, shaped like mortar-bepinex-bridge's: <c>gameVersion</c>, <c>gameVersionSource</c>
/// and <c>plugins</c> (<c>guid</c>, <c>version</c>). SMAPI has no scene and exposes no mod folder, so those keys are
/// absent. It never carries the overlay or command token.</summary>
internal static class StatusReply
{
    internal const string Line = "status";

    internal static string Build(string? gameVersion, IEnumerable<(string Guid, string Version)> mods)
    {
        var reply = new Dictionary<string, object>();
        if (gameVersion != null)
        {
            reply["gameVersion"] = gameVersion;
            reply["gameVersionSource"] = "game";
        }

        reply["plugins"] = mods.Select(mod => new Dictionary<string, string> { ["guid"] = mod.Guid, ["version"] = mod.Version }).ToArray();
        return JsonSerializer.Serialize(reply);
    }
}
