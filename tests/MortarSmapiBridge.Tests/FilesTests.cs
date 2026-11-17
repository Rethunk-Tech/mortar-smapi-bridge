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

    [Fact]
    public void AtomicWriteRestrictsTheTempFileBeforeItHoldsTheSecret()
    {
        if (OperatingSystem.IsWindows())
            return;

        string dir = Path.Join(Path.GetTempPath(), "mortar-state-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Join(dir, "mortar-smapi-bridge.json");
            UnixFileMode? modeBeforeContents = null;
            string? contentsBeforeRestrict = null;
            Files.AtomicWrite(path, "token", temp =>
            {
                modeBeforeContents = UnixMode(temp);
                contentsBeforeRestrict = File.ReadAllText(temp);
                Files.Restrict(temp);
            });
            Assert.Equal("", contentsBeforeRestrict);
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, modeBeforeContents);
            Assert.Equal("token", File.ReadAllText(path));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, UnixMode(path));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    private static UnixFileMode UnixMode(string path)
    {
        if (OperatingSystem.IsWindows())
            return UnixFileMode.UserRead | UnixFileMode.UserWrite;
        return File.GetUnixFileMode(path);
    }
}
