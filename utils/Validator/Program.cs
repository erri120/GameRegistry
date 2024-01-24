using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NLog;
using NLog.Targets;

namespace Validator;

public static class Program
{
    public static async Task Main(string[] args)
    {
        LogManager.Setup().LoadConfiguration(builder =>
        {
            builder.ForLogger().FilterMinLevel(
#if DEBUG
                LogLevel.Debug
#else
                LogLevel.Info
#endif
            ).WriteToColoredConsole();
            builder.ForLogger().FilterMinLevel(LogLevel.Trace).WriteToDebug();
            builder.ForLogger().FilterMinLevel(LogLevel.Trace).WriteToFile(
                fileName: "log.log",
                encoding: Encoding.UTF8,
                lineEnding: LineEndingMode.LF,
                keepFileOpen: true,
                concurrentWrites: false
            );
        });

        try
        {
            // TODO: signals
            var failed = await Runner.RunAsync(cancellationToken: CancellationToken.None);
            Environment.ExitCode = failed ? 1 : 0;
        }
        catch (Exception e)
        {
            LogManager.GetLogger(nameof(Program)).Fatal(e);
            Environment.ExitCode = 1;
        }
        finally
        {
            LogManager.Shutdown();
        }
    }
}
