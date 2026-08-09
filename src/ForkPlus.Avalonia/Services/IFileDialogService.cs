using System.Threading.Tasks;
using Avalonia.Controls;

namespace ForkPlus.Services
{
	/// <summary>
	/// 跨平台文件/目录选择对话框服务（对标原 WPF 工程 ForkPlus/UI/OpenDialog）。
	/// 原实现依赖 Windows API Code Pack 的 CommonOpenFileDialog / CommonSaveFileDialog（仅 Windows），
	/// 此处用 Avalonia 原生 OpenFileDialog / SaveFileDialog / OpenFolderDialog 替代，
	/// 行为一一对应，且模态于传入的父窗口（对标 OpenDialog.ShowDialog 的 parent 参数）。
	/// </summary>
	public interface IFileDialogService
	{
		/// <summary>选择目录（对标 OpenDialog.SelectDirectory，IsFolderPicker=true）。</summary>
		Task<string?> SelectDirectoryAsync(Window? parent, string title, string? initialDirectory);

		/// <summary>选择可执行文件（对标 OpenDialog.SelectExecutableFile，过滤器 "*.exe"）。</summary>
		Task<string?> SelectExecutableFileAsync(Window? parent, string title, string? initialDirectory);

		/// <summary>选择单个文件，fileTypeName 为过滤器名称、extensionPattern 形如 "*.cs"（对标 OpenDialog.SelectFile）。</summary>
		Task<string?> SelectFileAsync(Window? parent, string title, string? initialDirectory, string fileTypeName, string extensionPattern);

		/// <summary>选择补丁保存位置，自动补 .patch 扩展名（对标 OpenDialog.SelectPatchSaveLocation）。</summary>
		Task<string?> SelectPatchSaveLocationAsync(Window? parent, string title, string? initialDirectory, string defaultFileName);

		/// <summary>选择任意文件保存位置（对标 OpenDialog.SelectFileSaveLocation）。</summary>
		Task<string?> SelectFileSaveLocationAsync(Window? parent, string title, string? initialDirectory, string defaultFileName);
	}
}
