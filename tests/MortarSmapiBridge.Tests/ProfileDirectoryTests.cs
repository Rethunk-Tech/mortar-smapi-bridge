using MortarSmapiBridge.Startup;
using Xunit;

namespace MortarSmapiBridge.Tests;

public class ProfileDirectoryTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(2, true)]
    [InlineData(1, false)]
    public void FindReturnsTheNearestAncestorHoldingProfileJson(int depth, bool placeProfile)
    {
        string root = Directory.CreateTempSubdirectory("profile-dir").FullName;
        try
        {
            string current = root;
            for (int i = 0; i < depth; i++)
                current = Directory.CreateDirectory(Path.Combine(current, $"mods{i}")).FullName;

            if (placeProfile)
                File.WriteAllText(Path.Combine(root, "profile.json"), "{}");
            else
                File.WriteAllText(Path.Combine(current, "other.json"), "{}");

            string? found = ProfileDirectory.Find(current);
            Assert.Equal(placeProfile ? root : null, found);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
