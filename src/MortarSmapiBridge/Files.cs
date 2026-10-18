using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MortarSmapiBridge;

internal static class Files
{
    // restrict runs on the still-empty temp file, so its contents are never readable by others.
    internal static void AtomicWrite(string path, string contents, Action<string>? restrict = null)
    {
        string temp = path + ".tmp";
        if (restrict != null)
        {
            File.WriteAllText(temp, "");
            restrict(temp);
        }
        File.WriteAllText(temp, contents);
        File.Move(temp, path, overwrite: true);
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

    private static void RestrictWindows(string path)
    {
        string account = Environment.UserDomainName + "\\" + Environment.UserName;
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "icacls",
            Arguments = $"\"{path}\" /inheritance:r /grant:r \"{account}:(F)\"",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        });
        if (process == null)
            throw new IOException($"Could not restrict permissions on {path}.");
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new IOException($"Could not restrict permissions on {path}: {process.StandardError.ReadToEnd().Trim()}");
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int chmod(string path, uint mode);
}
