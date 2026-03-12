using System;
using System.Diagnostics;
using System.IO;
using UniCli.Protocol;

namespace UniCli.Client;

internal static class UnityLauncher
{
    public static string? FindUnityEditorPath(string projectRoot)
    {
        var version = ReadEditorVersion(projectRoot);
        if (version == null)
            return null;

        string editorPath;
        if (OperatingSystem.IsMacOS())
        {
            editorPath = $"/Applications/Unity/Hub/Editor/{version}/Unity.app/Contents/MacOS/Unity";
        }
        else if (OperatingSystem.IsWindows())
        {
            editorPath = $@"C:\Program Files\Unity\Hub\Editor\{version}\Editor\Unity.exe";
        }
        else if (OperatingSystem.IsLinux())
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            editorPath = Path.Combine(home, "Unity", "Hub", "Editor", version, "Editor", "Unity");
        }
        else
        {
            return null;
        }

        return File.Exists(editorPath) ? editorPath : null;
    }

    public static Result<bool, string> Launch(string projectRoot, UnityLaunchOptions launchOptions)
    {
        var editorPath = FindUnityEditorPath(projectRoot);
        if (editorPath == null)
        {
            var version = ReadEditorVersion(projectRoot);
            var versionMsg = version != null
                ? $"Unity {version} is not installed at the expected Hub location."
                : "Could not read editor version from ProjectSettings/ProjectVersion.txt.";
            return Result<bool, string>.Error(versionMsg);
        }

        var normalizedRoot = NormalizeProjectRoot(projectRoot);
        var startInfo = CreateStartInfo(editorPath, normalizedRoot, OperatingSystem.IsMacOS(), launchOptions);

        using var process = Process.Start(startInfo);
        if (process == null)
            return Result<bool, string>.Error("Failed to start Unity Editor process.");

        return Result<bool, string>.Success(true);
    }

    internal static ProcessStartInfo CreateStartInfo(
        string editorPath,
        string projectRoot,
        bool isMacOS,
        UnityLaunchOptions launchOptions)
    {
        if (launchOptions.IsHeadless)
            return CreateHeadlessStartInfo(editorPath, projectRoot, launchOptions.NoGraphics);

        if (isMacOS)
        {
            var appBundlePath = TryGetMacOSAppBundlePath(editorPath);
            if (appBundlePath != null)
                return CreateMacOSOpenStartInfo(appBundlePath, projectRoot);
        }

        return CreateDirectStartInfo(editorPath, projectRoot);
    }

    internal static string? ReadEditorVersion(string projectRoot)
    {
        var normalizedRoot = NormalizeProjectRoot(projectRoot);
        var versionFilePath = Path.Combine(normalizedRoot, "ProjectSettings", "ProjectVersion.txt");

        if (!File.Exists(versionFilePath))
            return null;

        try
        {
            foreach (var line in File.ReadLines(versionFilePath))
            {
                if (!line.StartsWith("m_EditorVersion:"))
                    continue;

                var colonIndex = line.IndexOf(':');
                if (colonIndex < 0)
                    continue;

                return line.Substring(colonIndex + 1).Trim();
            }
        }
        catch
        {
            // Best-effort: silently ignore read errors
        }

        return null;
    }

    internal static string? TryGetMacOSAppBundlePath(string editorPath)
    {
        var macOsDirectory = Path.GetDirectoryName(editorPath);
        var contentsDirectory = macOsDirectory != null
            ? Path.GetDirectoryName(macOsDirectory)
            : null;
        var appBundlePath = contentsDirectory != null
            ? Path.GetDirectoryName(contentsDirectory)
            : null;

        if (string.IsNullOrEmpty(appBundlePath))
            return null;

        return string.Equals(Path.GetExtension(appBundlePath), ".app", StringComparison.OrdinalIgnoreCase)
            ? appBundlePath
            : null;
    }

    internal static string NormalizeProjectRoot(string projectRoot)
    {
        var trimmed = projectRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var normalized = Path.GetFileName(trimmed) == "Assets"
            ? Path.GetDirectoryName(trimmed) ?? trimmed
            : trimmed;
        return Path.GetFullPath(normalized);
    }

    internal static string GetHeadlessLogPath(string projectRoot)
    {
        var normalizedRoot = NormalizeProjectRoot(projectRoot);
        return Path.Combine(normalizedRoot, "Library", "UniCli", "headless.log");
    }

    private static ProcessStartInfo CreateMacOSOpenStartInfo(string appBundlePath, string projectRoot)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "open",
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("-a");
        startInfo.ArgumentList.Add(appBundlePath);
        startInfo.ArgumentList.Add("--args");
        startInfo.ArgumentList.Add("-projectPath");
        startInfo.ArgumentList.Add(projectRoot);
        return startInfo;
    }

    private static ProcessStartInfo CreateDirectStartInfo(string editorPath, string projectRoot)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = editorPath,
            UseShellExecute = true,
        };
        startInfo.ArgumentList.Add("-projectPath");
        startInfo.ArgumentList.Add(projectRoot);
        return startInfo;
    }

    private static ProcessStartInfo CreateHeadlessStartInfo(string editorPath, string projectRoot, bool noGraphics)
    {
        var logPath = GetHeadlessLogPath(projectRoot);
        var logDirectory = Path.GetDirectoryName(logPath);
        if (!string.IsNullOrEmpty(logDirectory))
            Directory.CreateDirectory(logDirectory);

        var startInfo = new ProcessStartInfo
        {
            FileName = editorPath,
            UseShellExecute = true,
        };
        startInfo.ArgumentList.Add("-projectPath");
        startInfo.ArgumentList.Add(projectRoot);
        startInfo.ArgumentList.Add("-batchmode");
        startInfo.ArgumentList.Add("-accept-apiupdate");
        if (noGraphics)
            startInfo.ArgumentList.Add("-nographics");
        startInfo.ArgumentList.Add("-logFile");
        startInfo.ArgumentList.Add(logPath);
        return startInfo;
    }
}
