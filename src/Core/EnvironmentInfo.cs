using System.Runtime.InteropServices;

namespace Core;

public static class EnvironmentInfo
{
    public static EnvironmentReport Collect()
    {
#if NET10_0_OR_GREATER
        const string targetNote = ".NET 10.0 (Сучасний реліз)";
#elif NET8_0_OR_GREATER
        const string targetNote = ".NET 8.0 (LTS)";
#else
        const string targetNote = "Старіша версія .NET";
#endif

        return new EnvironmentReport(
            RuntimeInformation.OSDescription,
            RuntimeInformation.FrameworkDescription,
            RuntimeInformation.ProcessArchitecture.ToString(),
            DetectRid(),
            RuntimeInformation.RuntimeIdentifier,
            AppContext.BaseDirectory,
            targetNote);
    }

    private static string DetectRid()
    {
        string os =
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win" :
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux" :
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "osx" : "unknown";

        string arch = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.X86 => "x86",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm",
            _ => "unknown"
        };

        return $"{os}-{arch}";
    }
}