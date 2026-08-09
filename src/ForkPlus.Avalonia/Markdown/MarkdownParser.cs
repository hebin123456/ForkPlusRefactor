using System;
using System.Collections.Generic;
using System.Text;

namespace ForkPlus.Avalonia.Markdown
{
	/// <summary>
	/// v3.9.0 跨平台迁移（P4）：轻量 Markdown 文档模型。
	/// 对标原 WPF 工程中 WebView2 + biturbo <c>bt_md_to_html</c> 的「Markdown→HTML」中间产物——
	/// 此处直接把 Markdown 解析为块级/行内模型，由 <see cref="AvaloniaMarkdownViewer"/> 渲染为 Avalonia 控件树，
	/// 彻底甩掉 WebView2（Windows-only）与 biturbo 的 HTML 中间层，零外部依赖。
	/// 覆盖 AI 输出高频语法：标题、段落、围栏代码块、有序/无序列表、引用、表格、分隔线，
	/// 以及行内的粗体、斜体、行内代码、链接。
	/// </summary>

	#region 行内模型

	/// <summary>行内元素类型。</summary>
	public enum MdInlineKind
	{
		Text,
		Bold,
		Italic,
		Code,
		Link
	}

	/// <summary>行内元素：文本 / 粗体 / 斜体 / 行内代码 / 链接。</summary>
	public sealed class MdInline
	{
		public MdInlineKind Kind { get; }
		public string Text { get; }
		public string Url { get; }
		public List<MdInline> Children { get; }

		private MdInline(MdInlineKind kind, string text, string url, List<MdInline> children)
		{
			Kind = kind;
			Text = text;
			Url = url;
			Children = children;
		}

		public static MdInline CreateText(string t) => new MdInline(MdInlineKind.Text, t, null, null);
		public static MdInline CreateCode(string t) => new MdInline(MdInlineKind.Code, t, null, null);
		public static MdInline CreateLink(string t, string url) => new MdInline(MdInlineKind.Link, t, url, null);

		public static MdInline CreateBold(List<MdInline> children) => new MdInline(MdInlineKind.Bold, null, null, children);
		public static MdInline CreateItalic(List<MdInline> children) => new MdInline(MdInlineKind.Italic, null, null, children);
	}

	#endregion

	#region 块级模型

	public enum MdBlockKind
	{
		Heading,
		Paragraph,
		Code,
		List,
		Quote,
		Table,
		Rule
	}

	/// <summary>块级元素。所有具体块共享此基类。</summary>
	public abstract class MdBlock
	{
		public MdBlockKind Kind { get; }

		protected MdBlock(MdBlockKind kind) => Kind = kind;
	}

	public sealed class MdHeading : MdBlock
	{
		public int Level { get; }
		public List<MdInline> Inlines { get; }

		public MdHeading(int level, List<MdInline> inlines) : base(MdBlockKind.Heading)
		{
			Level = level;
			Inlines = inlines;
		}
	}

	public sealed class MdParagraph : MdBlock
	{
		public List<MdInline> Inlines { get; }

		public MdParagraph(List<MdInline> inlines) : base(MdBlockKind.Paragraph) => Inlines = inlines;
	}

	public sealed class MdCode : MdBlock
	{
		public string Language { get; }
		public string Code { get; }

		public MdCode(string language, string code) : base(MdBlockKind.Code)
		{
			Language = language;
			Code = code;
		}
	}

	public sealed class MdList : MdBlock
	{
		public bool Ordered { get; }
		public List<List<MdInline>> Items { get; }

		public MdList(bool ordered, List<List<MdInline>> items) : base(MdBlockKind.List)
		{
			Ordered = ordered;
			Items = items;
		}
	}

	public sealed class MdQuote : MdBlock
	{
		public List<MdInline> Inlines { get; }

		public MdQuote(List<MdInline> inlines) : base(MdBlockKind.Quote) => Inlines = inlines;
	}

	public sealed class MdTable : MdBlock
	{
		public List<MdInline> Header { get; }
		public List<List<MdInline>> Rows { get; }

		public MdTable(List<MdInline> header, List<List<MdInline>> rows) : base(MdBlockKind.Table)
		{
			Header = header;
			Rows = rows;
		}
	}

	public sealed class MdRule : MdBlock
	{
		public MdRule() : base(MdBlockKind.Rule)
		{
		}
	}

	/// <summary>解析后的 Markdown 文档（块级序列）。</summary>
	public sealed class MdDocument
	{
		public List<MdBlock> Blocks { get; } = new List<MdBlock>();

		public static MdDocument Parse(string markdown) => MarkdownParser.Parse(markdown);
	}

	#endregion

	/// <summary>
	/// 轻量 Markdown 解析器（AI 输出子集）。对标原工程中 <c>bt_md_to_html</c> 的转换职责，
	/// 但产物是强类型模型而非 HTML 字符串，便于直接映射到 Avalonia 控件。
	/// </summary>
	public static class MarkdownParser
	{
		public static MdDocument Parse(string markdown)
		{
			var doc = new MdDocument();
			if (string.IsNullOrEmpty(markdown))
			{
				return doc;
			}

			string[] lines = markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
			int i = 0;
			while (i < lines.Length)
			{
				string line = lines[i];

				// 空行：块分隔
				if (string.IsNullOrWhiteSpace(line))
				{
					i++;
					continue;
				}

				// 围栏代码块 ``` 或 ~~~
				if (line.TrimStart().StartsWith("```") || line.TrimStart().StartsWith("~~~"))
				{
					int fenceLen = line.TrimStart().StartsWith("```") ? 3 : 3;
					char fence = line.TrimStart().StartsWith("```") ? '`' : '~';
					string language = line.TrimStart().Substring(fenceLen).Trim();
					var code = new StringBuilder();
					i++;
					while (i < lines.Length)
					{
						string cl = lines[i];
						if (cl.Trim().Length >= fenceLen && cl.Trim().StartsWith(new string(fence, fenceLen))
							&& cl.Trim().IndexOfAny(new[] { '`', '~' }) == cl.Trim().IndexOf(fence))
						{
							i++;
							break;
						}
						code.Append(cl).Append('\n');
						i++;
					}
					doc.Blocks.Add(new MdCode(language, code.ToString().TrimEnd('\n')));
					continue;
				}

				// 分隔线 --- 或 *** （独占一行）
				string trimmed = line.Trim();
				if (IsHorizontalRule(trimmed))
				{
					doc.Blocks.Add(new MdRule());
					i++;
					continue;
				}

				// 标题 # .. ######
				if (trimmed.StartsWith("#"))
				{
					int level = 0;
					while (level < trimmed.Length && trimmed[level] == '#')
					{
						level++;
					}
					if (level <= 6 && (level == trimmed.Length || trimmed[level] == ' '))
					{
						string text = trimmed.Substring(level).Trim();
						doc.Blocks.Add(new MdHeading(level, ParseInlines(text)));
						i++;
						continue;
					}
				}

				// 表格：当前行是 |..| 且下一行是分隔符 |---|
				if (IsTableRow(trimmed) && i + 1 < lines.Length && IsTableSeparator(lines[i + 1].Trim()))
				{
					var header = SplitTableRow(trimmed);
					var rows = new List<List<MdInline>>();
					i += 2;
					while (i < lines.Length && IsTableRow(lines[i].Trim()))
					{
						rows.Add(SplitTableRow(lines[i].Trim()));
						i++;
					}
					doc.Blocks.Add(new MdTable(header, rows));
					continue;
				}

				// 引用 > （连续合并）
				if (trimmed.StartsWith(">"))
				{
					var sb = new StringBuilder();
					while (i < lines.Length && lines[i].TrimStart().StartsWith(">"))
					{
						sb.Append(lines[i].TrimStart().Substring(1).TrimStart()).Append(' ');
						i++;
					}
					doc.Blocks.Add(new MdQuote(ParseInlines(sb.ToString().Trim())));
					continue;
				}

				// 无序列表 - 或 *
				if (trimmed.StartsWith("- ") || trimmed.StartsWith("* ") || trimmed.StartsWith("+ "))
				{
					var items = new List<List<MdInline>>();
					while (i < lines.Length)
					{
						string l = lines[i].TrimStart();
						if (l.StartsWith("- ") || l.StartsWith("* ") || l.StartsWith("+ "))
						{
							items.Add(ParseInlines(l.Substring(2).Trim()));
							i++;
						}
						else
						{
							break;
						}
					}
					doc.Blocks.Add(new MdList(false, items));
					continue;
				}

				// 有序列表 1. 2. ...
				if (OrderedListPrefix(trimmed))
				{
					var items = new List<List<MdInline>>();
					while (i < lines.Length)
					{
						string l = lines[i].TrimStart();
						if (OrderedListPrefix(l))
						{
							int dot = l.IndexOf('.');
							items.Add(ParseInlines(l.Substring(dot + 1).Trim()));
							i++;
						}
						else
						{
							break;
						}
					}
					doc.Blocks.Add(new MdList(true, items));
					continue;
				}

				// 段落（连续非空、非块起始行合并）
				var para = new StringBuilder();
				while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i])
					&& !IsBlockStart(lines[i].Trim()))
				{
					para.Append(lines[i].Trim()).Append(' ');
					i++;
				}
				doc.Blocks.Add(new MdParagraph(ParseInlines(para.ToString().Trim())));
			}

			return doc;
		}

		#region 行内解析

		/// <summary>
		/// 解析行内元素。支持行内代码 `code`、粗体 **bold**、斜体 *italic*、链接 [text](url)。
		/// 粗体/斜体内可嵌套其它行内元素。未闭合的标记当作普通文本处理（容错）。
		/// </summary>
		public static List<MdInline> ParseInlines(string text)
		{
			var result = new List<MdInline>();
			if (string.IsNullOrEmpty(text))
			{
				return result;
			}

			int i = 0;
			var buffer = new StringBuilder();
			while (i < text.Length)
			{
				char c = text[i];

				// 行内代码
				if (c == '`')
				{
					int close = text.IndexOf('`', i + 1);
					if (close >= 0)
					{
						FlushText(result, buffer);
						result.Add(MdInline.CreateCode(text.Substring(i + 1, close - i - 1)));
						i = close + 1;
						continue;
					}
				}

				// 粗体 **
				if (c == '*' && i + 1 < text.Length && text[i + 1] == '*')
				{
					int close = text.IndexOf("**", i + 2, StringComparison.Ordinal);
					if (close >= 0)
					{
						FlushText(result, buffer);
						result.Add(MdInline.CreateBold(ParseInlines(text.Substring(i + 2, close - i - 2))));
						i = close + 2;
						continue;
					}
				}

				// 斜体 *
				if (c == '*')
				{
					int close = text.IndexOf('*', i + 1);
					if (close >= 0)
					{
						FlushText(result, buffer);
						result.Add(MdInline.CreateItalic(ParseInlines(text.Substring(i + 1, close - i - 1))));
						i = close + 1;
						continue;
					}
				}

				// 链接 [text](url)
				if (c == '[')
				{
					int closeBracket = text.IndexOf(']', i + 1);
					if (closeBracket >= 0 && closeBracket + 1 < text.Length && text[closeBracket + 1] == '(')
					{
						int closeParen = text.IndexOf(')', closeBracket + 2);
						if (closeParen >= 0)
						{
							FlushText(result, buffer);
							string linkText = text.Substring(i + 1, closeBracket - i - 1);
							string url = text.Substring(closeBracket + 2, closeParen - closeBracket - 2);
							result.Add(MdInline.CreateLink(linkText, url));
							i = closeParen + 1;
							continue;
						}
					}
				}

				buffer.Append(c);
				i++;
			}

			FlushText(result, buffer);
			return result;
		}

		private static void FlushText(List<MdInline> result, StringBuilder buffer)
		{
			if (buffer.Length > 0)
			{
				result.Add(MdInline.CreateText(buffer.ToString()));
				buffer.Clear();
			}
		}

		#endregion

		#region 辅助判定

		private static bool IsHorizontalRule(string trimmed)
		{
			if (trimmed.Length < 3)
			{
				return false;
			}
			char first = trimmed[0];
			if (first != '-' && first != '*' && first != '_')
			{
				return false;
			}
			foreach (char ch in trimmed)
			{
				if (ch != first)
				{
					return false;
				}
			}
			return true;
		}

		private static bool IsTableRow(string trimmed)
		{
			return !string.IsNullOrWhiteSpace(trimmed)
				&& trimmed.StartsWith("|", StringComparison.Ordinal)
				&& trimmed.EndsWith("|", StringComparison.Ordinal);
		}

		private static bool IsTableSeparator(string trimmed)
		{
			if (!IsTableRow(trimmed))
			{
				return false;
			}
			string value = trimmed.Trim().Trim('|').Replace("|", "").Replace("-", "").Replace(":", "").Trim();
			return value.Length == 0;
		}

		private static List<MdInline> SplitTableRow(string trimmed)
		{
			string[] cells = trimmed.Trim().Trim('|').Split('|');
			var result = new List<MdInline>();
			foreach (string cell in cells)
			{
				result.Add(ParseInlines(cell.Trim()));
			}
			return result;
		}

		private static bool OrderedListPrefix(string trimmed)
		{
			int j = 0;
			while (j < trimmed.Length && char.IsDigit(trimmed[j]))
			{
				j++;
			}
			return j > 0 && j < trimmed.Length && trimmed[j] == '.' && j + 1 < trimmed.Length && trimmed[j + 1] == ' ';
		}

		/// <summary>判断某行是否是新的块级起始（用于段落合并边界）。</summary>
		private static bool IsBlockStart(string trimmed)
		{
			if (string.IsNullOrWhiteSpace(trimmed))
			{
				return true;
			}
			if (trimmed.StartsWith("#"))
			{
				int level = 0;
				while (level < trimmed.Length && trimmed[level] == '#')
				{
					level++;
				}
				return level <= 6 && (level == trimmed.Length || trimmed[level] == ' ');
			}
			if (trimmed.StartsWith("```") || trimmed.StartsWith("~~~"))
			{
				return true;
			}
			if (IsHorizontalRule(trimmed))
			{
				return true;
			}
			if (trimmed.StartsWith(">"))
			{
				return true;
			}
			if (trimmed.StartsWith("- ") || trimmed.StartsWith("* ") || trimmed.StartsWith("+ "))
			{
				return true;
			}
			if (OrderedListPrefix(trimmed))
			{
				return true;
			}
			if (IsTableRow(trimmed))
			{
				return true;
			}
			return false;
		}

		#endregion
	}
}
