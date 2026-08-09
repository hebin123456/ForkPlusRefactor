using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ForkPlus.Services;

namespace ForkPlus.Services.AvaloniaImpl
{
	/// <summary>
	/// 跨平台文件/目录对话框实现（对标原 WPF 工程 ForkPlus/UI/OpenDialog）。
	/// 用 Avalonia 12 的 <see cref="IStorageProvider"/>（TopLevel.StorageProvider）替代 Windows API Code Pack 的
	/// CommonOpenFileDialog / CommonSaveFileDialog；方法签名与行为一一对应，并模态于传入的父窗口。
	/// 注意：Avalonia 12 已移除旧版 OpenFileDialog/SaveFileDialog/OpenFolderDialog，统一改用 IStorageProvider。
	/// </summary>
	public class AvaloniaFileDialogService : IFileDialogService
	{
		public async Task<string?> SelectDirectoryAsync(Window? parent, string title, string? initialDirectory)
		{
			var provider = Provider(parent);
			if (provider == null)
				return null;
			var folders = await provider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title, AllowMultiple = false });
			return folders.FirstOrDefault()?.TryGetLocalPath();
		}

		public async Task<string?> SelectExecutableFileAsync(Window? parent, string title, string? initialDirectory)
			=> await SelectFileAsync(parent, title, initialDirectory, "Applications", "*.exe");

		public async Task<string?> SelectFileAsync(Window? parent, string title, string? initialDirectory, string fileTypeName, string extensionPattern)
		{
			var provider = Provider(parent);
			if (provider == null)
				return null;
			var ext = NormalizeExt(extensionPattern);
			var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
			{
				Title = title,
				AllowMultiple = false,
				FileTypeFilter = new List<FilePickerFileType>
				{
					new FilePickerFileType(fileTypeName) { Patterns = new List<string> { ext } }
				}
			});
			return files.FirstOrDefault()?.TryGetLocalPath();
		}

		public async Task<string?> SelectPatchSaveLocationAsync(Window? parent, string title, string? initialDirectory, string defaultFileName)
		{
			var provider = Provider(parent);
			if (provider == null)
				return null;
			var file = await provider.SaveFilePickerAsync(new FilePickerSaveOptions
			{
				Title = title,
				SuggestedFileName = defaultFileName,
				DefaultExtension = "patch",
				FileTypeChoices = new List<FilePickerFileType>
				{
					new FilePickerFileType("Patches") { Patterns = new List<string> { "patch" } }
				}
			});
			var path = file?.TryGetLocalPath();
			if (path != null && !path.EndsWith(".patch", StringComparison.OrdinalIgnoreCase))
				path += ".patch";
			return path;
		}

		public async Task<string?> SelectFileSaveLocationAsync(Window? parent, string title, string? initialDirectory, string defaultFileName)
		{
			var provider = Provider(parent);
			if (provider == null)
				return null;
			var ext = Path.GetExtension(defaultFileName).TrimStart('.');
			var file = await provider.SaveFilePickerAsync(new FilePickerSaveOptions
			{
				Title = title,
				SuggestedFileName = defaultFileName,
				DefaultExtension = ext,
				FileTypeChoices = new List<FilePickerFileType>
				{
					new FilePickerFileType($"*{ext} files") { Patterns = new List<string> { ext } }
				}
			});
			return file?.TryGetLocalPath();
		}

		private static IStorageProvider? Provider(Window? parent) => parent?.StorageProvider;

		private static string NormalizeExt(string pattern)
		{
			var s = pattern.Trim();
			if (s.StartsWith("*."))
				s = s.Substring(2);
			else if (s.StartsWith("."))
				s = s.Substring(1);
			return s;
		}
	}
}
