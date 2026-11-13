using Xunit;

namespace MortarSmapiBridge.Tests;

public class FilesTests
{
    [Fact]
    public void DeleteIfUnchangedKeepsAFileAnotherInstanceRewrote()
    {
        string path = Path.Join(Path.GetTempPath(), "mortar-state-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.WriteAllText(path, "other");
            Files.DeleteIfUnchanged(path, "mine");
            Assert.True(File.Exists(path));
            File.WriteAllText(path, "mine");
            Files.DeleteIfUnchanged(path, "mine");
            Assert.False(File.Exists(path));
            Files.DeleteIfUnchanged(path, "mine");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
