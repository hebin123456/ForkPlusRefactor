using Avalonia;

namespace ForkPlus.Avalonia;

public class Program
{
	// 跨平台入口：Windows / Linux / macOS 共用同一套代码（不再依赖 WPF）。
	[STAThread]
	public static void Main(string[] args)
	{
		BuildAvaloniaApp()
			.StartWithClassicDesktopLifetime(args);
	}

	public static AppBuilder BuildAvaloniaApp()
		=> AppBuilder.Configure<App>()
			.UsePlatformDetect()
			.WithInterFont()
			.LogToTrace();
}
