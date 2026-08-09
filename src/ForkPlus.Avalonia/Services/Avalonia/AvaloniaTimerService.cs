using System;
using Avalonia.Threading;
using ForkPlus.Services;

namespace ForkPlus.Services.Avalonia
{
	/// <summary>
	/// 跨平台定时器，直接使用 Avalonia 的 DispatcherTimer（与原 WPF DispatcherTimer 行为一致：在 UI 线程触发 Tick）。
	/// </summary>
	public class AvaloniaTimerService : ITimerService
	{
		private readonly DispatcherTimer _timer;

		public TimeSpan Interval
		{
			get => _timer.Interval;
			set => _timer.Interval = value;
		}

		public bool IsEnabled => _timer.IsEnabled;

		public event EventHandler Tick;

		public AvaloniaTimerService()
		{
			_timer = new DispatcherTimer();
			_timer.Tick += (sender, e) => Tick?.Invoke(this, e);
		}

		public AvaloniaTimerService(TimeSpan interval) : this()
		{
			_timer.Interval = interval;
		}

		public void Start() => _timer.Start();

		public void Stop() => _timer.Stop();

		public void Dispose()
		{
			_timer.Stop();
		}
	}
}
