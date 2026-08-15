using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using ForkPlus.Avalonia.Diff;

namespace ForkPlus.Avalonia.Git;

/// <summary>
/// M3：把 <c>git diff &lt;parent&gt; &lt;child&gt;</c>（或 <c>git show &lt;commit&gt;</c>）产出的
/// unified diff 文本解析成与 <see cref="LineDiff"/>（M0 LCS 算法）同构的
/// <see cref="DiffResult"/>。
///
/// <para>
/// 解析对象对应原 WPF 中 <c>ForkPlus.Git.Diff.VisualDiff</c> 的输入：每个文件一个
/// <c>Chunk</c> 序列，每行有 <c>Kind</c>（Context/Add/Delete）+ <c>OldLine</c>/<c>NewLine</c>
/// 行号。这里我们把多文件 patch 拍平为单个 <see cref="DiffLine"/> 列表（每个文件头之间
/// 插入一行特殊 Unchanged 行作为文件分隔符），渲染层 <see cref="ForkPlus.Avalonia.Diff.DiffView"/>
/// 无需关心文件边界。
/// </para>
///
/// <para>
/// 不依赖 git diff 的 <c>--unified=0</c> / <c>-U0</c> 等特殊格式；输入必须是标准 unified
/// diff 文本（带 <c>@@ -a,b +c,d @@</c> hunk 头）。
/// </para>
/// </summary>
public static class UnifiedDiffParser
{
    private static readonly Regex HunkHeader = new(
        @"^@@ -(?<oldStart>\d+)(,(?<oldCount>\d+))? \+(?<newStart>\d+)(,(?<newCount>\d+))? @@",
        RegexOptions.Compiled);

    /// <summary>
    /// 解析 unified diff 文本为 <see cref="DiffResult"/>。
    /// </summary>
    /// <param name="unifiedDiffText">git diff 的标准输出（含多个文件，每个含若干 @@ hunk）。</param>
    /// <param name="fallbackOldPath">解析不到任何文件头时使用的旧路径。</param>
    /// <param name="fallbackNewPath">解析不到任何文件头时使用的新路径。</param>
    public static DiffResult Parse(string unifiedDiffText,
        string fallbackOldPath = "a/file",
        string fallbackNewPath = "b/file")
    {
        if (unifiedDiffText == null)
        {
            throw new ArgumentNullException(nameof(unifiedDiffText));
        }

        var lines = new List<DiffLine>();
        string oldPath = fallbackOldPath;
        string newPath = fallbackNewPath;
        bool sawAnyFileHeader = false;
        int? curOldStart = null;
        int? curNewStart = null;
        int curOldLine = 0;
        int curNewLine = 0;

        using var sr = new StringReader(unifiedDiffText);
        string? line;
        while ((line = sr.ReadLine()) != null)
        {
            if (line.StartsWith("diff --git ", StringComparison.Ordinal))
            {
                // diff --git a/<p1> b/<p2>
                (oldPath, newPath) = ParseDiffGitHeader(line);
                if (sawAnyFileHeader)
                {
                    // 上一个文件结束，插一行 Unchanged 文件分隔符（前后各空一行）
                    lines.Add(new DiffLine(DiffLineKind.Unchanged, $"── {oldPath} ──", -1, -1));
                }
                else
                {
                    sawAnyFileHeader = true;
                }
                curOldStart = curNewStart = null;
                continue;
            }
            if (line.StartsWith("--- ", StringComparison.Ordinal))
            {
                oldPath = TrimDiffPathPrefix(line.Substring(4));
                continue;
            }
            if (line.StartsWith("+++ ", StringComparison.Ordinal))
            {
                newPath = TrimDiffPathPrefix(line.Substring(4));
                continue;
            }
            if (line.StartsWith("@@", StringComparison.Ordinal))
            {
                Match m = HunkHeader.Match(line);
                if (!m.Success)
                {
                    // 不是合法 hunk 头，跳过避免误把 "index ..." 当行号 0
                    continue;
                }
                curOldStart = int.Parse(m.Groups["oldStart"].Value);
                curOldLine = curOldStart.Value;
                curNewStart = int.Parse(m.Groups["newStart"].Value);
                curNewLine = curNewStart.Value;
                continue;
            }
            if (curOldStart == null || curNewStart == null)
            {
                // hunk 之前的元数据行（"index ..."、二进制标记等），跳过
                continue;
            }

            // hunk 内容
            if (line.Length == 0)
            {
                // 空行：视为 " " 前缀（未变），但保留长度 0
                lines.Add(new DiffLine(DiffLineKind.Unchanged, "", curOldLine, curNewLine));
                curOldLine++;
                curNewLine++;
                continue;
            }
            char prefix = line[0];
            string body = line.Substring(1);
            switch (prefix)
            {
                case '+':
                    lines.Add(new DiffLine(DiffLineKind.Added, body, -1, curNewLine));
                    curNewLine++;
                    break;
                case '-':
                    lines.Add(new DiffLine(DiffLineKind.Removed, body, curOldLine, -1));
                    curOldLine++;
                    break;
                case ' ':
                    lines.Add(new DiffLine(DiffLineKind.Unchanged, body, curOldLine, curNewLine));
                    curOldLine++;
                    curNewLine++;
                    break;
                case '\\':
                    // "\ No newline at end of file" — git 元数据，跳过
                    break;
                default:
                    // 不可识别前缀（极少），按 Unchanged 兜底，避免吞掉关键行
                    lines.Add(new DiffLine(DiffLineKind.Unchanged, line, curOldLine, curNewLine));
                    curOldLine++;
                    curNewLine++;
                    break;
            }
        }

        return new DiffResult(oldPath, newPath, lines);
    }

    /// <summary>
    /// 从 <c>diff --git a/foo b/bar</c> 提取 <c>foo</c> / <c>bar</c>。
    /// </summary>
    private static (string OldPath, string NewPath) ParseDiffGitHeader(string header)
    {
        // 形如：diff --git a/path/to/file b/path/to/file
        // 名字两侧的 "a/"/"b/" 是 git 约定，不是路径的一部分（实际可能含空格 / 引号场景下 git 会用引号转义）
        const string marker = "diff --git ";
        string body = header.Substring(marker.Length);
        int mid = FindSplitIndex(body);
        string left = body.Substring(0, mid).Trim();
        string right = body.Substring(mid + 1).Trim();
        left = StripPrefix(left, "a/");
        right = StripPrefix(right, "b/");
        return (left, right);
    }

    private static int FindSplitIndex(string body)
    {
        // 在 "a/<p1> b/<p2>" 里找中间的空格，要求它是 "a/... b/..." 的真分隔
        // 简化：找第一个未加引号、未转义的空格，且左半部以 "a/" 开头
        if (!body.StartsWith("a/") && !body.StartsWith("\"a/"))
        {
            // 退化：用第一个空格
            int idx = body.IndexOf(' ');
            return idx < 0 ? 0 : idx;
        }
        int i = 0;
        bool inQuote = false;
        while (i < body.Length)
        {
            char c = body[i];
            if (c == '"')
            {
                inQuote = !inQuote;
                i++;
                continue;
            }
            if (c == ' ' && !inQuote)
            {
                return i;
            }
            i++;
        }
        return body.Length;
    }

    private static string StripPrefix(string s, string prefix)
    {
        if (s.Length >= 2 && s[0] == '"' && s[s.Length - 1] == '"')
        {
            s = s.Substring(1, s.Length - 2);
        }
        if (s.StartsWith(prefix, StringComparison.Ordinal))
        {
            return s.Substring(prefix.Length);
        }
        return s;
    }

    private static string TrimDiffPathPrefix(string raw)
    {
        // "a/foo.cs" 或 "/dev/null"
        string p = raw.Trim();
        if (p == "/dev/null")
        {
            return p;
        }
        if (p.StartsWith("a/", StringComparison.Ordinal))
        {
            return p.Substring(2);
        }
        if (p.StartsWith("b/", StringComparison.Ordinal))
        {
            return p.Substring(2);
        }
        return p;
    }
}
