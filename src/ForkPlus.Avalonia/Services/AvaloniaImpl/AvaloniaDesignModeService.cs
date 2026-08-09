using Avalonia;
using Avalonia.Controls;
using ForkPlus.Services;

namespace ForkPlus.Services.AvaloniaImpl
{
	/// <summary>
	/// 跨平台设计时检测。Avalonia 无 WPF 的 DesignerProperties，
	/// 使用 Avalonia 自带的 Design.IsDesignMode 附加属性（运行时恒为 false）。
	/// </summary>
	public class AvaloniaDesignModeService : IDesignModeService
	{
		public bool IsInDesignMode => Design.IsDesignMode;
	}
}
