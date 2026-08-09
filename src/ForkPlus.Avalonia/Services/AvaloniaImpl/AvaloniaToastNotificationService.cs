using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ForkPlus.Services;

namespace ForkPlus.Services.AvaloniaImpl
{
	/// <summary>
	/// 跨平台 Toast 通知实现。
	/// 入参 xmlPayload 为原工程构造的 WinRT Toast XML
	/// （见 ForkPlus/Accounts/NotificationManager.SendWindowsNotification：
	///  &lt;toast launch="..."&gt;&lt;visual&gt;&lt;binding template="ToastGeneric"&gt;&lt;text&gt;...&lt;/text&gt;...&lt;/binding&gt;&lt;/visual&gt;&lt;/toast&gt;）。
	/// 派发策略：
	///   - Linux：优先 notify-send（libnotify），失败回退 in-app 浮层。
	///   - macOS：优先 osascript display notification，失败回退 in-app 浮层。
	///   - Windows：当前 net10.0 单 TFM 工程无法引用 WinRT Toast 包（仅 Windows TFM 可用），
	///     故直接用 in-app 浮层；生产可在 Windows TFM 分支接 WinRT ToastNotification。
	///   - in-app 浮层（ToastWindow）作为所有平台的保底，保证通知一定可见。
	/// </summary>
	public class AvaloniaToastNotificationService : IToastNotificationService
	{
		public void Show(string xmlPayload)
		{
			try
			{
				var (title, body, launch) = Parse(xmlPayload);
				if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(body))
					return;

				if (!TryShowNative(title, body))
					ShowToastWindow(title, body, launch);
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine("AvaloniaToastNotificationService.Show failed: " + ex.Message);
			}
		}

		private static (string Title, string Body, string Launch) Parse(string xmlPayload)
		{
			var doc = XDocument.Parse(xmlPayload);
			var texts = doc.Descendants()
				.Where(e => e.Name.LocalName == "text")
				.Select(e => e.Value)
				.ToArray();
			var title = texts.Length > 0 ? texts[0] : "";
			var body = texts.Length > 1 ? string.Join("\n", texts.Skip(1)) : "";
			var launch = doc.Descendants()
				.FirstOrDefault(e => e.Name.LocalName == "toast")?
				.Attribute("launch")?.Value;
			return (title, body, launch);
		}

		/// <summary>尝试用平台原生通知；成功返回 true，否则返回 false 走兜底。</summary>
		private static bool TryShowNative(string title, string body)
		{
			try
			{
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
				{
					var psi = new ProcessStartInfo
					{
						FileName = "notify-send",
						Arguments = $"{(title.Length > 0 ? "\"" + title.Replace("\"", "\\\"") + "\"" : "")} \"{body.Replace("\"", "\\\"")}\"",
						UseShellExecute = false,
						CreateNoWindow = true,
						RedirectStandardError = true
					};
					using var p = Process.Start(psi);
					p?.WaitForExit(2000);
					// 退出码 0 = 已派发（或 notify-send 不存在时通常非 0）
					return p != null && p.ExitCode == 0;
				}

				if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
				{
					var script = $"display notification \"{body.Replace("\"", "\\\"")}\" with title \"{title.Replace("\"", "\\\"")}\"";
					var psi = new ProcessStartInfo
					{
						FileName = "osascript",
						Arguments = $"-e '{script}'",
						UseShellExecute = false,
						CreateNoWindow = true,
						RedirectStandardError = true
					};
					using var p = Process.Start(psi);
					p?.WaitForExit(2000);
					return p != null && p.ExitCode == 0;
				}
			}
			catch
			{
				// 原生通道不可用（命令缺失/权限），回退 in-app
			}
			return false;
		}

		private static void ShowToastWindow(string title, string body, string launch)
		{
			if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime)
				return;
			var window = new ToastWindow(title, body);
			window.Show(); // 非模态浮层，3 秒后自动关闭
		}
	}
}
