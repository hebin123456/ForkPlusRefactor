using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using ForkPlus.Services;

namespace ForkPlus.Services.AvaloniaImpl
{
	/// <summary>
	/// 跨平台 Shell 集成实现。
	/// Windows：通过 shell32.dll 原生 API 设置 AppUserModelID、加入最近文件。
	/// 其他平台：全部为安全 no-op（跳转列表/任务栏分组是 Windows 专属能力，
	/// 对标 App.xaml.cs NativeMethods.SetAppUserModelID 在 Wine 下直接崩溃的坑）。
	/// </summary>
	public class AvaloniaShellIntegrationService : IShellIntegrationService
	{
		public bool JumpListSupported => OperatingSystem.IsWindows();

		public void SetAppUserModelId(string appUserModelId)
		{
			if (!OperatingSystem.IsWindows())
				return; // 非 Windows：降级，避免调用不存在的原生库
			try
			{
				NativeMethods.SetCurrentProcessExplicitAppUserModelID(appUserModelId);
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine("SetAppUserModelId failed: " + ex.Message);
			}
		}

		public void AddRecentFile(string filePath)
		{
			if (!OperatingSystem.IsWindows())
				return;
			try
			{
				NativeMethods.SHAddToRecentDocs(filePath);
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine("AddRecentFile failed: " + ex.Message);
			}
		}

		// shell32 原生方法集中在 Windows-only 的嵌套类中，确保 P/Invoke 声明不会在非 Windows 被触发。
		[SupportedOSPlatform("windows")]
		private static class NativeMethods
		{
			[DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
			internal static extern void SetCurrentProcessExplicitAppUserModelID(
				[MarshalAs(UnmanagedType.LPWStr)] string appId);

			[DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
			internal static extern void SHAddToRecentDocs(
				uint flags,
				[MarshalAs(UnmanagedType.LPWStr)] string path);

			// flags=2 (SHARD_PATHW)
			internal static void SHAddToRecentDocs(string path) => SHAddToRecentDocs(2, path);
		}
	}
}
