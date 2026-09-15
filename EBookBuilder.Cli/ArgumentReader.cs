using System.Diagnostics.CodeAnalysis;

namespace Lpubsppop01.EBookBuilder.Cli;

/// <summary>Reads command line arguments.</summary>
/// <remarks>
/// Only the shapes actually needed are handled here, so that no external parser library
/// has to be brought in. This is a small tool and dependencies are best kept few,
/// so this is enough.
/// </remarks>
public sealed class ArgumentReader
{
    readonly Dictionary<string, string?> m_Options = new(StringComparer.Ordinal);

    ArgumentReader(Dictionary<string, string?> options) => m_Options = options;

    /// <summary>Parses arguments into a form that accepts both <c>--name value</c> and <c>--flag</c>.</summary>
    public static ArgumentReader Parse(IReadOnlyList<string> args)
    {
        var options = new Dictionary<string, string?>(StringComparer.Ordinal);

        for (var i = 0; i < args.Count; ++i)
        {
            var arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException($"Options must start with --: {arg}");

            var name = arg[2..];
            if (name.Length == 0) throw new ArgumentException("The option name is empty.");

            // If the next argument is another option, this one is a flag with no value.
            var hasValue = i + 1 < args.Count && !args[i + 1].StartsWith("--", StringComparison.Ordinal);
            if (hasValue)
            {
                options[name] = args[i + 1];
                ++i;
            }
            else
            {
                options[name] = null;
            }
        }

        return new ArgumentReader(options);
    }

    /// <summary>Gets a required option that takes a value.</summary>
    public string RequireValue(string name)
    {
        if (!m_Options.TryGetValue(name, out var value) || value is null)
            throw new ArgumentException($"Specify --{name}.");
        return value;
    }

    /// <summary>Gets an optional option that takes a value.</summary>
    public string? GetValue(string name) =>
        m_Options.TryGetValue(name, out var value) ? value : null;

    /// <summary>Gets a required integer value.</summary>
    public int RequireInt(string name)
    {
        var text = RequireValue(name);
        if (!int.TryParse(text, out var value))
            throw new ArgumentException($"--{name} requires an integer: {text}");
        return value;
    }

    /// <summary>Gets an optional integer value. <paramref name="defaultValue"/> is used when not specified.</summary>
    public int GetInt(string name, int defaultValue)
    {
        var text = GetValue(name);
        if (text is null) return defaultValue;
        if (!int.TryParse(text, out var value))
            throw new ArgumentException($"--{name} requires an integer: {text}");
        return value;
    }

    /// <summary>Whether a flag with no value was specified.</summary>
    public bool HasFlag(string name) => m_Options.ContainsKey(name);

    /// <summary>Returns all specified option names.</summary>
    public IEnumerable<string> Names => m_Options.Keys;

    /// <summary>Throws when any name other than the allowed ones was specified.</summary>
    public void RejectUnknown(params string[] allowed)
    {
        var allowedSet = new HashSet<string>(allowed, StringComparer.Ordinal);
        foreach (var name in m_Options.Keys)
        {
            if (!allowedSet.Contains(name))
                throw new ArgumentException($"Unknown option: --{name}");
        }
    }

    /// <summary>Checks that all required options are present.</summary>
    public void RequireAll(params string[] names)
    {
        foreach (var name in names)
        {
            if (!m_Options.TryGetValue(name, out var value) || value is null)
                throw new ArgumentException($"Specify --{name}.");
        }
    }

    [DoesNotReturn]
    public static void Fail(string message) => throw new ArgumentException(message);
}
