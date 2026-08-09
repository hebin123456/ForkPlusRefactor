using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace ForkPlus.Services.AvaloniaImpl
{
	/// <summary>
	/// 轻量 in-app Toast 浮层（MVP）。纯代码构造，无需 XAML。
	/// 用于承载 AvaloniaToastNotificationService 解析出的 WinRT Toast 文本。
	/// </summary>
	public class ToastWindow : Window
	{
		public ToastWindow(string title, string body)
		{
			Title = title;
			Width = 320;
			Height = 96;
			WindowStartupLocation = WindowStartupLocation.CenterScreen;
			WindowDecorations = global::Avalonia.Controls.WindowDecorations.None;
			Topmost = true;
			Background = new SolidColorBrush(Color.Parse("#2d2d30"));

			var panel = new StackPanel
			{
				Margin = new Thickness(12),
				Spacing = 4
			};
			panel.Children.Add(new TextBlock
			{
				Text = title,
				FontWeight = FontWeight.Bold,
				Foreground = Brushes.White
			});
			if (!string.IsNullOrEmpty(body))
			{
				panel.Children.Add(new TextBlock
				{
					Text = body,
					Foreground = Brushes.White,
					TextWrapping = TextWrapping.Wrap
				});
			}

			Content = panel;

			var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
			timer.Tick += (_, _) =>
			{
				timer.Stop();
				Close();
			};
			timer.Start();
		}
	}
}
