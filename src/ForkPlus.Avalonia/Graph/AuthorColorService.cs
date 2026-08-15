using System;
using System.Collections.Generic;
using Avalonia.Media;
using ForkPlus.Avalonia.Graph;

namespace ForkPlus.Avalonia.Graph;

/// <summary>
/// M2 作者色点：author 字符串 → 一致颜色。
/// 用 SHA1-ish 哈希 + HSV 分布（饱和度 0.6，明度 0.85，色相按字符展开），
/// 同一作者永远同色，不同作者差异够大。
///
/// <para>对标 WPF v3.9.0 截图 03-demo-graph.png 的作者列：每个 author 名前面有一个
/// 圆形色点（"tester" 是红色）。Avalonia 端用同一作者同色的语义，颜色在
/// <see cref="CommitGraphLayout.LanePalette"/> 同色系内取，避免和 lane 撞色。
/// </para>
/// </summary>
public static class AuthorColorService
{
	// 同一作者 → 同一颜色；进程内缓存
	private static readonly Dictionary<string, Color> _cache = new(StringComparer.Ordinal);

	public static Color GetColor(string? author)
	{
		if (string.IsNullOrEmpty(author)) return Color.FromRgb(0x80, 0x80, 0x80); // 未知名：灰
		if (_cache.TryGetValue(author, out var cached)) return cached;

		// 把 author 名字当成字符串做 FNV-1a 32-bit
		uint hash = 2166136261u;
		foreach (char c in author)
		{
			hash ^= c;
			hash *= 16777619u;
		}
		// 映射到 HSV：色相 = hash % 360
		float hue = (hash % 360u) / 360f;
		var color = HsvToRgb(hue, saturation: 0.65f, value: 0.85f);
		_cache[author] = color;
		return color;
	}

	private static Color HsvToRgb(float h, float saturation, float value)
	{
		float c = value * saturation;
		float x = c * (1f - Math.Abs((h * 6f) % 2f - 1f));
		float m = value - c;
		float r, g, b;
		if (h < 1f / 6f) { r = c; g = x; b = 0; }
		else if (h < 2f / 6f) { r = x; g = c; b = 0; }
		else if (h < 3f / 6f) { r = 0; g = c; b = x; }
		else if (h < 4f / 6f) { r = 0; g = x; b = c; }
		else if (h < 5f / 6f) { r = x; g = 0; b = c; }
		else { r = c; g = 0; b = x; }
		return Color.FromRgb(
			(byte)Math.Clamp((r + m) * 255, 0, 255),
			(byte)Math.Clamp((g + m) * 255, 0, 255),
			(byte)Math.Clamp((b + m) * 255, 0, 255));
	}
}
