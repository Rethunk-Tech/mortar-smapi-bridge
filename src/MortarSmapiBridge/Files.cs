using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MortarSmapiBridge;

internal static class Files
{
    // restrict runs on the still-empty temp file, so its contents are never readable by others.
    // File.WriteAllText creates 0666&~umask; another local user can keep that 0644 fd through chmod and read the token.
    internal static void AtomicWrite(string path, string contents, Action<string>? restrict = null)
    {
        string temp = path + ".tmp";
        try
        {
            if (restrict != null)
            {
                CreateOwnerOnly(temp);
                restrict(temp);
            }
            File.WriteAllText(temp, contents);
            File.Move(temp, path, overwrite: true);
        }
        catch
        {
            try
            {
                File.Delete(temp);
            }
            catch (IOException ex)
            {
                Debug.WriteLine(ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.WriteLine(ex);
            }
            throw;
        }
    }

    // A second game instance may have rewritten the file with its own port and token; that one is still live.
    internal static void DeleteIfUnchanged(string path, string contents)
    {
        try
        {
            if (File.ReadAllText(path) == contents)
                File.Delete(path);
        }
        catch (FileNotFoundException)
        {
        }
        catch (DirectoryNotFoundException)
        {
        }
    }

    internal static void Restrict(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            RestrictWindows(path);
            return;
        }

        if (chmod(path, 0x180) != 0)
            throw new IOException($"Could not restrict permissions on {path}.");
    }

    private static void CreateOwnerOnly(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            // FileStream copies FileOptions into CreateFile dwFlagsAndAttributes.
            using var created = new FileStream(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1,
                (FileOptions)FileFlagOpenReparsePoint);
            return;
        }

        int fd = open(path, UnixCreateFlags(), 0x180);
        if (fd < 0)
            throw new IOException($"Could not restrict permissions on {path}.");
        try
        {
            if (fchmod(fd, 0x180) != 0)
                throw new IOException($"Could not restrict permissions on {path}.");
        }
        finally
        {
            _ = close(fd);
        }
    }

    // open(2) bits are per OS; Linux x86_64 and arm64 share asm-generic fcntl.h.
    internal const int LinuxCreateFlags = 1 | 0x40 | 0x80 | 0x200 | 0x20000; // O_WRONLY|O_CREAT|O_EXCL|O_TRUNC|O_NOFOLLOW
    internal const int DarwinCreateFlags = 1 | 0x0200 | 0x0800 | 0x0400 | 0x0100;
    internal const uint FileFlagOpenReparsePoint = 0x00200000;

    internal static int UnixCreateFlags()
    {
        if (OperatingSystem.IsLinux())
            return LinuxCreateFlags;
        if (OperatingSystem.IsMacOS())
            return DarwinCreateFlags;
        throw new PlatformNotSupportedException();
    }

    private static void RestrictWindows(string path)
    {
        string account = Environment.UserDomainName + "\\" + Environment.UserName;
        var start = new ProcessStartInfo
        {
            FileName = Path.Join(Environment.SystemDirectory, "icacls.exe"),
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add(path);
        start.ArgumentList.Add("/inheritance:r");
        start.ArgumentList.Add("/grant:r");
        start.ArgumentList.Add(account + ":(F)");
        using var process = Process.Start(start);
        if (process == null)
            throw new IOException($"Could not restrict permissions on {path}.");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(10_000))
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException ex)
            {
                Debug.WriteLine(ex);
            }
            try
            {
                Task.WhenAll(stdout, stderr).GetAwaiter().GetResult();
            }
            catch (IOException ex)
            {
                Debug.WriteLine(ex);
            }
            catch (ObjectDisposedException ex)
            {
                Debug.WriteLine(ex);
            }
            catch (AggregateException ex)
            {
                Debug.WriteLine(ex);
            }
            throw new IOException($"Could not restrict permissions on {path}: timed out.");
        }
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new IOException($"Could not restrict permissions on {path}: {stderr.GetAwaiter().GetResult().Trim()}");
        _ = stdout.GetAwaiter().GetResult();
    }

    // SMAPI runs on .NET 6, which has no File.SetUnixFileMode; starting a chmod process from inside the game is worse.
    [DllImport("libc", SetLastError = true)]
    private static extern int chmod(string path, uint mode);

    [DllImport("libc", SetLastError = true)]
    private static extern int open(string path, int flags, int mode);

    [DllImport("libc", SetLastError = true)]
    private static extern int fchmod(int fd, uint mode);

    [DllImport("libc", SetLastError = true)]
    private static extern int close(int fd);
}
