namespace ForkPlus.Services
{
	/// <summary>
	/// 跨平台 Shell 集成服务（任务栏/跳转列表/最近文件）。
	/// 对标原 WPF 工程 App.xaml.cs 中 shell32.dll 的 AppUserModelID 设置，
	/// 以及 Windows API Code Pack 的跳转列表 / 文件关联。
	/// 这些能力本质是 Windows 专属：非 Windows 平台安全降级为 no-op，避免 Wine 下崩溃。
	/// </summary>
	public interface IShellIntegrationService
	{
		/// <summary>显式设置当前进程的 AppUserModelID（任务栏分组）。仅 Windows 生效。</summary>
		void SetAppUserModelId(string appUserModelId);

		/// <summary>当前平台是否支持跳转列表/AppUserModelID（仅 Windows 为 true）。</summary>
		bool JumpListSupported { get; }

		/// <summary>将文件加入系统"最近使用"（Windows 跳转列表）。非 Windows 为 no-op。</summary>
		void AddRecentFile(string filePath);
	}
}
