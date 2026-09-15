using Lpubsppop01.EBookBuilder.Core;

namespace Lpubsppop01.EBookBuilder.Cli;

/// <summary>Shows progress by overwriting a single line.</summary>
/// <remarks>
/// Overwriting is not possible when the output is redirected, so nothing is written then.
/// This keeps control characters out of pipes and files.
/// </remarks>
public sealed class ConsoleProgress : IProgress<PageProgress>
{
    readonly string m_Verb;
    bool m_Reported;

    public ConsoleProgress(string verb) => m_Verb = verb;

    public void Report(PageProgress value)
    {
        if (Console.IsOutputRedirected) return;

        m_Reported = true;
        Console.Write($"\r{value.Done} / {value.Total} ({value.Percentage}%) {m_Verb}".PadRight(SafeWidth()));
    }

    /// <summary>Commits the overwritten line so that the next output follows on its own line.</summary>
    public void Complete()
    {
        if (m_Reported) Console.WriteLine();
    }

    /// <summary>The terminal width. A default value is used where it cannot be obtained.</summary>
    static int SafeWidth()
    {
        try
        {
            var width = Console.WindowWidth;
            return width > 0 ? width - 1 : 79;
        }
        catch (IOException)
        {
            return 79;
        }
        catch (PlatformNotSupportedException)
        {
            return 79;
        }
    }
}
