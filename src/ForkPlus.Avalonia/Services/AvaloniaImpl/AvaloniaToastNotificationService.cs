using System;
using System.Linq;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using ForkPlus.Services;

namespace ForkPlus.Services.AvaloniaImpl
{
	/// <summary>
	/// 跨平台 Toast 通知实现（MVP：in-app 浮层）。
	/// 入参 xmlPayload 为原工程构造的 WinRT Toast XML
	/// （见 ForkPlus/Accounts/NotificationManager.SendWindowsNotification：
	///  &lt;toast launch="..."&gt;&lt;visual&gt;&lt;binding template="ToastGeneric"&gt;&lt;text&gt;...&lt;/text&gt;...&lt;/binding&gt;&lt;/visual&gt;&lt;/toast&gt;）。
	/// 这里忠实解析该 XML，提取 &lt;text&gt; 与 launch 参数，以跨平台浮层显示；
	/// 后续可替换为各平台原生通知（macOS NSUserNotification / Linux libnotify / Windows Toast）。
	/// </summary>
	public class AvaloniaToastNotificationService : IToastNotificationService
	{
		public void Show(string xmlPayload)
		{
			try
			{
				var doc = XDocument.Parse(xmlPayload);
				var texts = doc.Descendants()
					.Where(e => e.Name.LocalName == "text")
					.Select(e => e.Value)
					.ToArray();
				if (texts.Length == 0)
					return;

				var title = texts[0];
				var body = texts.Length > 1 ? string.Join("\n", texts.Skip(1)) : "";
				var launch = doc.Descendants()
					.FirstOrDefault(e => e.Name.LocalName == "toast")?
					.Attribute("launch")?.Value;

				ShowToastWindow(title, body, launch);
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine("AvaloniaToastNotificationService.Show failed: " + ex.Message);
			}
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
