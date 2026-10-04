using System.Reflection;
using HarmonyLib;

namespace MortarSmapiBridge.Startup;

/// <summary>Reads Harmony's patch registry for the methods each owner can replace outright: transpilers, and prefixes
/// returning bool (which can skip the original). Mortar compares the sets to spot mods doing the same job.</summary>
internal static class ReplacedMethods
{
    /// <summary>Harmony ID (by convention the mod's UniqueID) to its replaced methods as "Type.FullName::Method",
    /// sorted. Owners named <paramref name="ownId"/> or prefixed "<paramref name="ownId"/>." are left out.</summary>
    internal static Dictionary<string, List<string>> Collect(string ownId) =>
        From(Harmony.GetAllPatchedMethods().ToList().Select(m => (m, (Patches?)Harmony.GetPatchInfo(m))), ownId);

    internal static Dictionary<string, List<string>> From(IEnumerable<(MethodBase Method, Patches? Info)> patched, string ownId)
    {
        Dictionary<string, SortedSet<string>> byOwner = new(StringComparer.OrdinalIgnoreCase);
        foreach ((MethodBase method, Patches? info) in patched)
        {
            if (info == null)
                continue;
            string name = (method.DeclaringType?.FullName ?? "") + "::" + method.Name;
            foreach (Patch patch in info.Transpilers.Concat(info.Prefixes.Where(p => p.PatchMethod.ReturnType == typeof(bool))))
            {
                if (string.Equals(patch.owner, ownId, StringComparison.OrdinalIgnoreCase) || patch.owner.StartsWith(ownId + ".", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!byOwner.TryGetValue(patch.owner, out SortedSet<string>? set))
                    byOwner[patch.owner] = set = new SortedSet<string>(StringComparer.Ordinal);
                set.Add(name);
            }
        }
        return byOwner.ToDictionary(p => p.Key, p => p.Value.ToList(), StringComparer.OrdinalIgnoreCase);
    }
}
