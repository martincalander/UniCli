using ConsoleAppFramework;
using System.Threading.Tasks;

namespace UniCli.Client;

public partial class Commands
{
    /// <summary>
    /// Execute a command on the Unity Editor server
    /// </summary>
    public async Task<int> Exec(
        [Argument] string command,
        [Argument] string data = "",
        int timeout = 0,
        bool json = false,
        bool noFocus = false,
        bool headless = false,
        bool noGraphics = false)
    {
        var launchOptions = UnityLaunchOptions.Resolve(headless, noGraphics, noFocus);
        var result = await CommandExecutor.ExecuteAsync(command, data, timeout, json, launchOptions);
        return OutputWriter.Write(result, json);
    }
}
