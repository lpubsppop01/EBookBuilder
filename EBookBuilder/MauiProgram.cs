using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;

namespace Lpubsppop01.EBookBuilder;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseMauiCommunityToolkit()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		// Setup IMyFolderPicker
#if WINDOWS
		builder.Services.AddTransient<IMyFolderPicker, Platforms.Windows.MyFolderPicker>();
#elif MACCATALYST
		builder.Services.AddTransient<IMyFolderPicker, Platforms.MacCatalyst.MyFolderPicker>();
#endif
		builder.Services.AddTransient<MainPage>();

		return builder.Build();
	}
}
