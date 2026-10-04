namespace MortarSmapiBridge.Startup;

/// <summary>The Mortar profile a mod runs in: the nearest folder above the mod holding profile.json.</summary>
internal static class ProfileDirectory
{
    internal static string? Find(string modDirectory)
    {
        DirectoryInfo? current = new(modDirectory);
        while (current != null)
        {
            if (File.Exists(Path.Combine(current.FullName, "profile.json")))
                return current.FullName;
            current = current.Parent;
        }
        return null;
    }
}
