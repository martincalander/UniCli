using System;

namespace UniCli.Client;

internal enum UnityLaunchMode
{
    Interactive,
    Headless,
}

internal readonly record struct UnityLaunchOptions(
    UnityLaunchMode Mode,
    bool NoGraphics,
    bool FocusAllowed)
{
    public static UnityLaunchOptions DefaultInteractive => new(UnityLaunchMode.Interactive, false, false);

    public bool IsHeadless => Mode == UnityLaunchMode.Headless;

    public static UnityLaunchOptions Resolve(bool headlessFlag, bool noGraphicsFlag, bool noFocusFlag)
    {
        var mode = ResolveMode(headlessFlag);
        var focusAllowed = UnityProcessActivator.ShouldFocus(noFocusFlag, mode);
        return new UnityLaunchOptions(mode, noGraphicsFlag, focusAllowed);
    }

    internal static UnityLaunchMode ResolveMode(bool headlessFlag)
    {
        if (headlessFlag)
            return UnityLaunchMode.Headless;

        var mode = Environment.GetEnvironmentVariable("UNICLI_MODE");
        if (string.Equals(mode, "headless", StringComparison.OrdinalIgnoreCase))
            return UnityLaunchMode.Headless;

        return UnityLaunchMode.Interactive;
    }
}
