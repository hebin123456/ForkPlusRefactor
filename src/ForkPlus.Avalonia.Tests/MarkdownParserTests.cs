using System.Linq;
using ForkPlus.Avalonia.Markdown;
using Xunit;

namespace ForkPlus.Avalonia.Tests;

/// <summary>
/// P4 交付物回归测试：覆盖 MarkdownParser 的块级与行内解析（纯逻辑，无 Avalonia UI 依赖）。
/// 这是 P5「FlaUI → Avalonia UI 测试」迁移的第一步——先把与 WebView2/biturbo 解耦后的
/// 零依赖 Markdown 模型层用 xUnit 锁死，后续 headless UI 测试在其之上叠加。
///
/// 注意：行内元素统一为 <see cref="MdInline"/> 单类 + <see cref="MdInlineKind"/> 枚举区分，
/// 没有 MdBold/MdLink 等子类；块级元素才是 MdHeading/MdParagraph/... 等独立类型。
/// </summary>
public class MarkdownParserTests
{
	[Fact]
	public void Parse_NullOrEmpty_ReturnsEmptyDocument()
	{
		Assert.Empty(MdDocument.Parse(null!).Blocks);
		Assert.Empty(MdDocument.Parse("").Blocks);
		Assert.Empty(MdDocument.Parse("   \n  \n ").Blocks);
	}

	[Fact]
	public void Parse_Heading_LevelAndText()
	{
		var doc = MdDocument.Parse("# Hello World");
		var heading = Assert.Single(doc.Blocks).AsBlock<MdHeading>();
		Assert.Equal(1, heading.Level);
		Assert.Equal("Hello World", InlineText(heading.Inlines));
	}

	[Fact]
	public void Parse_Heading_Level3()
	{
		var doc = MdDocument.Parse("### Sub ## not a level");
		var heading = Assert.Single(doc.Blocks).AsBlock<MdHeading>();
		Assert.Equal(3, heading.Level);
		Assert.Equal("Sub ## not a level", InlineText(heading.Inlines));
	}

	[Fact]
	public void Parse_Paragraph_MergesConsecutiveLines()
	{
		var doc = MdDocument.Parse("first line\nsecond line\n\nnext paragraph");
		Assert.Equal(2, doc.Blocks.Count);
		var p = doc.Blocks[0].AsBlock<MdParagraph>();
		Assert.Equal("first line second line", InlineText(p.Inlines));
		Assert.Equal("next paragraph", InlineText(doc.Blocks[1].AsBlock<MdParagraph>().Inlines));
	}

	[Fact]
	public void Parse_FencedCodeBlock_WithLanguage()
	{
		var doc = MdDocument.Parse("```csharp\nvar x = 1;\nConsole.WriteLine(x);\n```");
		var code = Assert.Single(doc.Blocks).AsBlock<MdCode>();
		Assert.Equal("csharp", code.Language);
		Assert.Equal("var x = 1;\nConsole.WriteLine(x);", code.Code);
	}

	[Fact]
	public void Parse_UnorderedList()
	{
		var doc = MdDocument.Parse("- one\n- two");
		var list = Assert.Single(doc.Blocks).AsBlock<MdList>();
		Assert.False(list.Ordered);
		Assert.Equal(2, list.Items.Count);
		Assert.Equal("one", InlineText(list.Items[0]));
		Assert.Equal("two", InlineText(list.Items[1]));
	}

	[Fact]
	public void Parse_OrderedList()
	{
		var doc = MdDocument.Parse("1. first\n2. second");
		var list = Assert.Single(doc.Blocks).AsBlock<MdList>();
		Assert.True(list.Ordered);
		Assert.Equal(2, list.Items.Count);
		Assert.Equal("first", InlineText(list.Items[0]));
	}

	[Fact]
	public void Parse_Quote_MergesLines()
	{
		var doc = MdDocument.Parse("> quoted line one\n> quoted line two");
		var quote = Assert.Single(doc.Blocks).AsBlock<MdQuote>();
		Assert.Equal("quoted line one quoted line two", InlineText(quote.Inlines));
	}

	[Fact]
	public void Parse_Table_HeaderAndRows()
	{
		var doc = MdDocument.Parse("| A | B |\n| --- | --- |\n| 1 | 2 |\n| 3 | 4 |");
		var table = Assert.Single(doc.Blocks).AsBlock<MdTable>();
		Assert.Equal(2, table.Header.Count);
		Assert.Equal("A", InlineText(table.Header[0]));
		Assert.Equal(2, table.Rows.Count);
		Assert.Equal("1", InlineText(table.Rows[0][0]));
		Assert.Equal("4", InlineText(table.Rows[1][1]));
	}

	[Fact]
	public void Parse_HorizontalRule()
	{
		var doc = MdDocument.Parse("---");
		Assert.IsType<MdRule>(Assert.Single(doc.Blocks));
	}

	[Theory]
	[InlineData("***")]
	[InlineData("___")]
	public void Parse_HorizontalRule_Variants(string rule)
	{
		var doc = MdDocument.Parse(rule);
		Assert.IsType<MdRule>(Assert.Single(doc.Blocks));
	}

	[Fact]
	public void Parse_Inline_Bold()
	{
		var doc = MdDocument.Parse("a **bold** b");
		var p = Assert.Single(doc.Blocks).AsBlock<MdParagraph>();
		Assert.Equal(3, p.Inlines.Count);
		var bold = p.Inlines[1];
		Assert.Equal(MdInlineKind.Bold, bold.Kind);
		Assert.Equal("bold", InlineText(bold.Children));
	}

	[Fact]
	public void Parse_Inline_Italic()
	{
		var doc = MdDocument.Parse("a *it* b");
		var p = Assert.Single(doc.Blocks).AsBlock<MdParagraph>();
		var italic = p.Inlines[1];
		Assert.Equal(MdInlineKind.Italic, italic.Kind);
		Assert.Equal("it", InlineText(italic.Children));
	}

	[Fact]
	public void Parse_Inline_Code()
	{
		var doc = MdDocument.Parse("use `code` here");
		var p = Assert.Single(doc.Blocks).AsBlock<MdParagraph>();
		var code = p.Inlines[1];
		Assert.Equal(MdInlineKind.Code, code.Kind);
		Assert.Equal("code", code.Text);
	}

	[Fact]
	public void Parse_Inline_Link()
	{
		var doc = MdDocument.Parse("see [site](https://example.com) now");
		var p = Assert.Single(doc.Blocks).AsBlock<MdParagraph>();
		var link = p.Inlines[1];
		Assert.Equal(MdInlineKind.Link, link.Kind);
		Assert.Equal("site", link.Text);
		Assert.Equal("https://example.com", link.Url);
	}

	[Fact]
	public void Parse_Inline_BoldContainsNestedText()
	{
		var doc = MdDocument.Parse("**bold with spaces**");
		var p = Assert.Single(doc.Blocks).AsBlock<MdParagraph>();
		var bold = Assert.Single(p.Inlines);
		Assert.Equal(MdInlineKind.Bold, bold.Kind);
		Assert.Equal("bold with spaces", InlineText(bold.Children));
	}

	[Fact]
	public void Parse_UnclosedMarkers_TreatedAsText()
	{
		// 未闭合的 ** 应降级为普通文本，不应抛异常
		var doc = MdDocument.Parse("a **unclosed b");
		var p = Assert.Single(doc.Blocks).AsBlock<MdParagraph>();
		Assert.Equal("a **unclosed b", InlineText(p.Inlines));
	}

	[Fact]
	public void Parse_MixedDocument_ProducesExpectedBlockCount()
	{
		var md = "# Title\n\nIntro paragraph\n\n```js\nconsole.log(1);\n```\n\n- a\n- b\n\n> note\n\n| H1 | H2 |\n| --- | --- |\n| x | y |\n\n---";
		var doc = MdDocument.Parse(md);
		// Title, Intro, Code, List, Quote, Table, Rule = 7
		Assert.Equal(7, doc.Blocks.Count);
		Assert.Equal(MdBlockKind.Heading, doc.Blocks[0].Kind);
		Assert.Equal(MdBlockKind.Paragraph, doc.Blocks[1].Kind);
		Assert.Equal(MdBlockKind.Code, doc.Blocks[2].Kind);
		Assert.Equal(MdBlockKind.List, doc.Blocks[3].Kind);
		Assert.Equal(MdBlockKind.Quote, doc.Blocks[4].Kind);
		Assert.Equal(MdBlockKind.Table, doc.Blocks[5].Kind);
		Assert.Equal(MdBlockKind.Rule, doc.Blocks[6].Kind);
	}

	// ---- 断言辅助 ----

	private static string InlineText(List<MdInline> inlines)
	{
		var sb = new System.Text.StringBuilder();
		foreach (var inline in inlines)
		{
			switch (inline.Kind)
			{
				case MdInlineKind.Text:
				case MdInlineKind.Code:
					sb.Append(inline.Text);
					break;
				case MdInlineKind.Bold:
				case MdInlineKind.Italic:
					sb.Append(InlineText(inline.Children));
					break;
				case MdInlineKind.Link:
					sb.Append(inline.Text);
					break;
			}
		}
		return sb.ToString();
	}
}

// 块级元素强转扩展（MdHeading/MdParagraph/... 均为独立类型，派生自 MdBlock）
internal static class MarkdownAssertExtensions
{
	public static T AsBlock<T>(this MdBlock block) where T : MdBlock => Assert.IsType<T>(block);
}
