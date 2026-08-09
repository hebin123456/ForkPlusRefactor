using System;
using Avalonia.Threading;
using ForkPlus.Services;

namespace ForkPlus.Services.AvaloniaImpl
{
	/// <summary>
	/// 跨平台 UI 线程调度。对标 WPF 的 Dispatcher.BeginInvoke/Invoke。
	/// 若当前已在 UI 线程调用 Invoke，则直接执行（同 WPF Dispatcher.Invoke 语义，避免重入死锁）。
	/// </summary>
	public class AvaloniaDispatcher : IDispatcher
	{
		public void Post(Action action) => Dispatcher.UIThread.Post(action);

		public void Invoke(Action action)
		{
			if (Dispatcher.UIThread.CheckAccess())
				action();
			else
				Dispatcher.UIThread.InvokeAsync(action).GetAwaiter().GetResult();
		}
	}
}
