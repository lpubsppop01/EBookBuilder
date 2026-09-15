using System.Text.Json;
using System.Text.Json.Serialization;
using Lpubsppop01.EBookBuilder.Core.Build;
using Lpubsppop01.EBookBuilder.Core.Imaging;

namespace Lpubsppop01.EBookBuilder.App.Settings;

/// <summary>
/// The user's settings. Gathers what should be remembered from one run to the next.
/// </summary>
/// <remarks>
/// <para>
/// The original WPF version saved only the window position, using .NET Framework's
/// <c>ApplicationSettingsBase</c>. That mechanism is Windows-only, so it is replaced with a JSON file.
/// </para>
/// <para>
/// The default location is <c>ApplicationData</c>. On Linux this is <c>$XDG_CONFIG_HOME</c>
/// (usually <c>~/.config</c>), and on Windows it is <c>%APPDATA%</c>. Tests can substitute their own location.
/// </para>
/// <para>
/// In the original the build settings were lost on every run, but here they are saved along with the rest.
/// </para>
/// </remarks>
public sealed class AppSettings
{
    const string AppDirectoryName = "EBookBuilder";
    const string FileName = "settings.json";

    /// <summary>Window width.</summary>
    public double WindowWidth { get; set; } = 900;

    /// <summary>Window height.</summary>
    public double WindowHeight { get; set; } = 480;

    /// <summary>Left edge of the window. Null if not saved.</summary>
    public int? WindowX { get; set; }

    /// <summary>Top edge of the window. Null if not saved.</summary>
    public int? WindowY { get; set; }

    /// <summary>The folder that was open last time. Used as the initial location next time.</summary>
    public string? LastTargetDirectoryPath { get; set; }

    /// <summary>The image format to output.</summary>
    public BuildImageFormatKind ImageFormatKind { get; set; } = BuildImageFormatKind.Jpeg;

    /// <summary>How the output size is specified.</summary>
    public BuildSizeKind SizeKind { get; set; } = BuildSizeKind.Original;

    /// <summary>Width of the specified size.</summary>
    public int TargetWidth { get; set; } = 600;

    /// <summary>Height of the specified size.</summary>
    public int TargetHeight { get; set; } = 1024;

    /// <summary>Whether to draw the verification dots in the four corners.</summary>
    public bool DrawsCornerDots { get; set; }

    /// <summary>JPEG quality when re-encoding.</summary>
    public int JpegQuality { get; set; } = PageImagePipeline.DefaultJpegQuality;

    /// <summary>Creates settings that use the default file path.</summary>
    public AppSettings()
    {
    }

    /// <summary>Creates settings with the given file path.</summary>
    /// <param name="filePath">The file path.</param>
    public AppSettings(string filePath) => FilePath = filePath;

    /// <summary>The file path of these settings.</summary>
    [JsonIgnore]
    public string FilePath { get; set; } = DefaultFilePath;

    /// <summary>The default file path.</summary>
    public static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppDirectoryName, FileName);

    static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    /// <summary>Loads the settings. Returns the default values if the file is missing or broken.</summary>
    /// <param name="filePath">The file path. Defaults to the standard location when omitted.</param>
    public static AppSettings Load(string? filePath = null)
    {
        var path = filePath ?? DefaultFilePath;

        try
        {
            if (!File.Exists(path)) return new AppSettings { FilePath = path };

            var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), SerializerOptions);
            if (loaded is null) return new AppSettings { FilePath = path };

            // The file path is not written to the file, so it is set again after loading.
            loaded.FilePath = path;
            return loaded;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Let the app start even if the settings cannot be read
            return new AppSettings { FilePath = path };
        }
    }

    /// <summary>Saves the settings. Failures are not raised as exceptions.</summary>
    public void Save()
    {
        try
        {
            var directoryPath = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directoryPath)) Directory.CreateDirectory(directoryPath);

            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, SerializerOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Let the work continue even if the settings cannot be saved
        }
    }
}
