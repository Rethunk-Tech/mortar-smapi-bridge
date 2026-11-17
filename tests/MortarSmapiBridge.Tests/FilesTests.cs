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
    public void CreateFlagsAreExclusiveCreateOnThisPlatform()
    {
        Assert.Equal(1 | 0x40 | 0x80, Files.LinuxCreateFlags);
        Assert.Equal(1 | 0x0200 | 0x0800, Files.DarwinCreateFlags);

        if (OperatingSystem.IsLinux())
            Assert.Equal(Files.LinuxCreateFlags, Files.UnixCreateFlags());
        else if (OperatingSystem.IsMacOS())
            Assert.Equal(Files.DarwinCreateFlags, Files.UnixCreateFlags());
        else if (!OperatingSystem.IsWindows())
            Assert.Fail("no create-flag table for this OS");
    }

    [Fact]
    public void AtomicWriteDoesNotFollowASymlinkAtTheTempPath()
    {
        string dir = Path.Join(Path.GetTempPath(), "mortar-state-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string path = Path.Join(dir, "mortar-smapi-bridge.json");
            string victim = Path.Join(dir, "victim");
            File.WriteAllText(victim, "secret");
            try
            {
                File.CreateSymbolicLink(path + ".tmp", victim);
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }

            Assert.Throws<IOException>(() => Files.AtomicWrite(path, "token", Files.Restrict));
            Assert.Equal("secret", File.ReadAllText(victim));
            Assert.False(File.Exists(path));
        }
        finally
        {
            Directory.Delete(dir, true);
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
