using UniCli.Client;

namespace UniCli.Client.Tests;

public class UnityLaunchOptionsTests
{
    [Fact]
    public void HeadlessFlag_OverridesEnvironment()
    {
        Environment.SetEnvironmentVariable("UNICLI_MODE", "interactive");
        try
        {
            var options = UnityLaunchOptions.Resolve(headlessFlag: true, noGraphicsFlag: true, noFocusFlag: false);

            Assert.Equal(UnityLaunchMode.Headless, options.Mode);
            Assert.True(options.NoGraphics);
            Assert.False(options.FocusAllowed);
        }
        finally
        {
            Environment.SetEnvironmentVariable("UNICLI_MODE", null);
        }
    }

    [Fact]
    public void EnvironmentEnablesHeadlessMode()
    {
        Environment.SetEnvironmentVariable("UNICLI_MODE", "headless");
        try
        {
            var options = UnityLaunchOptions.Resolve(headlessFlag: false, noGraphicsFlag: false, noFocusFlag: false);

            Assert.Equal(UnityLaunchMode.Headless, options.Mode);
            Assert.False(options.FocusAllowed);
        }
        finally
        {
            Environment.SetEnvironmentVariable("UNICLI_MODE", null);
        }
    }

    [Fact]
    public void InteractiveMode_UsesFocusPolicy()
    {
        Environment.SetEnvironmentVariable("UNICLI_MODE", null);
        Environment.SetEnvironmentVariable("UNICLI_FOCUS", "true");
        try
        {
            var options = UnityLaunchOptions.Resolve(headlessFlag: false, noGraphicsFlag: false, noFocusFlag: false);

            Assert.Equal(UnityLaunchMode.Interactive, options.Mode);
            Assert.True(options.FocusAllowed);
        }
        finally
        {
            Environment.SetEnvironmentVariable("UNICLI_FOCUS", null);
        }
    }
}
