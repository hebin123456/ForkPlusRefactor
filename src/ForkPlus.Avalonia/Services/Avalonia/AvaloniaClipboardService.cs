using System;
using Avalonia;
using Avalonia.Input;
using Avalonia.Input.Platform;
using ForkPlus.Services;

namespace ForkPlus.Services.Avalonia
{
	/// <summary>
	/// 跨平台剪贴板实现。
	/// 原 WPF 实现（WpfClipboardService）使用同步 Clipboard.SetDataObject/GetData 并带重试；
	/// Avalonia 的 IClipboard 是异步 API，这里包装成接口要求的同步契约（业务层不变）。
	/// </summary>
	public class AvaloniaClipboardService : IClipboardService
	{
		public void SetText(string text)
		{
			text ??= "";
			try
			{
				GetClipboard()?.SetTextAsync(text).GetAwaiter().GetResult();
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine("AvaloniaClipboardService.SetText failed: " + ex.Message);
			}
		}

		public string GetText()
		{
			try
			{
				return GetClipboard()?.GetTextAsync().GetAwaiter().GetResult();
			}
			catch
			{
				return null;
			}
		}

		private static IClipboard? GetClipboard()
		{
			if (Application.Current?.ApplicationLifetime
				is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
			{
				return desktop.MainWindow?.Clipboard;
			}
			return null;
		}
	}
}
