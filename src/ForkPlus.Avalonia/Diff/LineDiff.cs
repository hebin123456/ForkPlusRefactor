using System;
using System.Collections.Generic;

namespace ForkPlus.Avalonia.Diff;

/// <summary>
/// 基于 LCS（最长公共子序列）的行级差异算法。
/// <para>
/// 这是跨平台可移植的等价实现，对应 ForkPlus 通过原生 <c>biturbo.dll</c> 计算的
/// <c>BtPatchToken</c>（kind + start/end 区间）。biturbo 是 Windows 原生库、无法在
/// Linux/macOS CI 与运行时使用，因此这里用纯托管算法产出与之同构的
/// <see cref="DiffLine"/> 序列；将来若提供跨平台 biturbo 构建，只需替换
/// <see cref="Compute"/> 的实现，渲染层（<see cref="DiffView"/>）无需改动。
/// </para>
/// </summary>
public static class LineDiff
{
    public static DiffResult Compute(string oldText, string newText, string oldPath = "a/file", string newPath = "b/file")
    {
        var a = SplitLines(oldText);
        var b = SplitLines(newText);

        int n = a.Length, m = b.Length;
        // dp[i,j] = LCS 长度(a[i..], b[j..])
        var dp = new int[n + 1, m + 1];
        for (int i = n - 1; i >= 0; i--)
            for (int j = m - 1; j >= 0; j--)
                dp[i, j] = a[i] == b[j] ? dp[i + 1, j + 1] + 1 : Math.Max(dp[i + 1, j], dp[i, j + 1]);

        var lines = new List<DiffLine>(n + m);
        int i2 = 0, j2 = 0, oldNo = 1, newNo = 1;
        while (i2 < n && j2 < m)
        {
            if (a[i2] == b[j2])
            {
                lines.Add(new DiffLine(DiffLineKind.Unchanged, a[i2], oldNo, newNo));
                i2++; j2++; oldNo++; newNo++;
            }
            else if (dp[i2 + 1, j2] >= dp[i2, j2 + 1])
            {
                lines.Add(new DiffLine(DiffLineKind.Removed, a[i2], oldNo, -1));
                i2++; oldNo++;
            }
            else
            {
                lines.Add(new DiffLine(DiffLineKind.Added, b[j2], -1, newNo));
                j2++; newNo++;
            }
        }
        while (i2 < n) { lines.Add(new DiffLine(DiffLineKind.Removed, a[i2], oldNo, -1)); i2++; oldNo++; }
        while (j2 < m) { lines.Add(new DiffLine(DiffLineKind.Added, b[j2], -1, newNo)); j2++; newNo++; }

        return new DiffResult(oldPath, newPath, lines);
    }

    private static string[] SplitLines(string text)
    {
        if (string.IsNullOrEmpty(text))
            return Array.Empty<string>();
        // 统一换行符，避免 \r\n vs \n 的差异扰乱比对
        var normalized = text.Replace("\r\n", "\n");
        if (normalized.EndsWith("\n"))
            normalized = normalized.Substring(0, normalized.Length - 1);
        return normalized.Split('\n');
    }
}
