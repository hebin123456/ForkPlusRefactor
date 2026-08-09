using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace ForkPlus.Avalonia.Markdown
{
	/// <summary>
	/// v3.9.0 跨平台迁移（P4）：AI 流式 Markdown 渲染控件。
	/// 对标原 WPF 工程的 <c>Ui.Controls.AiStreamingWebView</c>（WebView2 + biturbo HTML），
	/// 但完全用 Avalonia 原生控件树渲染，零外部依赖、跨平台：
	/// <list type="bullet">
	///   <item>Markdown→模型由 <see cref="MarkdownParser"/> 完成（替代 biturbo 的 bt_md_to_html），</item>
	///   <item>渲染由本控件完成（替代 WebView2 的 NavigateToString + CSS 主题 + 滚动跟随），</item>
	///   <item>主题跟随 <see cref="Application.RequestedThemeVariant"/> 天然实现（替代 WebView2 手动 PreferredColorScheme），</item>
	///   <item>滚动跟随由 ScrollViewer 实现（替代 WebView2 的 postMessage + ExecuteScriptAsync）。</item>
	/// </list>
	/// 公开 API 与 AiStreamingWebView 一一对应，便于将来把 AiTextResultWindow / AiCodeReviewWindow /
	/// GitMmReferenceWindow 直接切换到本控件。
	/// </summary>
	public sealed class AvaloniaMarkdownViewer : UserControl
	{
		private readonly ScrollViewer _scroll = new ScrollViewer
		{
			HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			Padding = new Thickness(12)
		};

		private readonly StackPanel _stack = new StackPanel
		{
			Spacing = 8
		};

		private readonly ProgressBar _busy = new ProgressBar
		{
			Width = 22,
			Height = 22,
			IsIndeterminate = true,
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center
		};

		private readonly TextBlock _fallback = new TextBlock
		{
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(16)
		};

		private readonly DispatcherTimer _throttleTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(400)
		};

		private StringBuilder _buffer = new StringBuilder();
		private bool _streaming;
		private bool _atBottom = true;

		/// <summary>流式进度：已接收字符数。</summary>
		public event Action<int> StreamingProgress;

		/// <summary>预留：替代 WebView2 的 WebMessageReceived（如 AI 代码审查 suggestion 按钮）。当前自研渲染不触发。</summary>
		public event Action<string> WebMessageReceived;

		/// <summary>链接点击（替代 HTML &lt;a&gt; 的导航）。调用方决定如何处理 URL。</summary>
		public event Action<string> LinkClicked;

		/// <summary>ForkPlus 风格：Markdown 源文本（设置即重渲染）。</summary>
		public static readonly StyledProperty<string> MarkdownProperty =
			AvaloniaProperty.Register<AvaloniaMarkdownViewer, string>(nameof(Markdown), string.Empty);

		public string Markdown
		{
			get => GetValue(MarkdownProperty);
			set => SetValue(MarkdownProperty, value);
		}

		public AvaloniaMarkdownViewer()
		{
			_scroll.Content = _stack;
			var grid = new Grid();
			grid.Children.Add(_scroll);
			grid.Children.Add(_busy);
			grid.Children.Add(_fallback);
			Content = grid;

			_scroll.ScrollChanged += (_, _) =>
			{
				double extent = _scroll.Extent.Height;
				double viewport = _scroll.Viewport.Height;
				double offset = _scroll.Offset.Y;
				_atBottom = viewport <= 0 || (offset + viewport >= extent - 6);
			};

			_throttleTimer.Tick += (_, _) =>
			{
				if (!_streaming)
				{
					return;
				}
				string md = _buffer.ToString();
				StreamingProgress?.Invoke(md.Length);
				Markdown = md;
				if (_atBottom)
				{
					_scroll.ScrollToEnd();
				}
			};

			ShowContent();
		}

		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == MarkdownProperty)
			{
				Rebuild();
			}
		}

		#region 流式 API（对标 AiStreamingWebView）

		public void StartStreaming()
		{
			_buffer = new StringBuilder();
			_streaming = true;
			_atBottom = true;
			if (!_throttleTimer.IsEnabled)
			{
				_throttleTimer.Start();
			}
		}

		public void ResumeStreaming()
		{
			_streaming = true;
			_atBottom = true;
			if (!_throttleTimer.IsEnabled)
			{
				_throttleTimer.Start();
			}
		}

		public void AppendChunk(string chunk)
		{
			if (string.IsNullOrEmpty(chunk) || !_streaming)
			{
				return;
			}
			_buffer.Append(chunk);
		}

		public void RenderFinal(string finalMarkdown = null)
		{
			_streaming = false;
			_throttleTimer.Stop();
			string md = finalMarkdown ?? _buffer.ToString();
			Markdown = md;
			if (_atBottom)
			{
				_scroll.ScrollToEnd();
			}
		}

		public void StopStreaming()
		{
			_streaming = false;
			_throttleTimer.Stop();
		}

		public string GetMarkdown() => _buffer.ToString();

		/// <summary>主题跟随由 Avalonia 自动处理；此处强制重渲染以刷新配色。</summary>
		public void UpdateTheme() => Rebuild();

		public void ShowBusy() => SwitchView(showScroll: false, showBusy: true, showFallback: false);

		public void ShowContent() => SwitchView(showScroll: true, showBusy: false, showFallback: false);

		public void ShowError(string message)
		{
			_fallback.Text = message ?? string.Empty;
			_fallback.Foreground = new SolidColorBrush(Color.Parse("#d33"));
			SwitchView(showScroll: false, showBusy: false, showFallback: true);
		}

		private void SwitchView(bool showScroll, bool showBusy, bool showFallback)
		{
			_scroll.IsVisible = showScroll;
			_busy.IsVisible = showBusy;
			_fallback.IsVisible = showFallback;
		}

		#endregion

		#region 渲染

		private void Rebuild()
		{
			MdDocument doc = MarkdownParser.Parse(Markdown);
			bool dark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
			Color fg = dark ? Color.Parse("#e4e4e4") : Color.Parse("#1e1e1e");
			Color codeBg = dark ? Color.Parse("#2d2d2d") : Color.Parse("#f4f4f4");
			Color border = dark ? Color.Parse("#4a4a4a") : Color.Parse("#dddddd");
			Color link = dark ? Color.Parse("#6aa9ff") : Color.Parse("#2678c8");

			_stack.Children.Clear();
			foreach (MdBlock block in doc.Blocks)
			{
				_stack.Children.Add(BuildBlock(block, fg, codeBg, border, link));
			}
		}

		private Control BuildBlock(MdBlock block, Color fg, Color codeBg, Color border, Color link)
		{
			switch (block.Kind)
			{
				case MdBlockKind.Heading:
					{
						var h = (MdHeading)block;
						int size = Math.Max(12, 26 - (h.Level - 1) * 3);
						return new TextBlock
						{
							TextWrapping = TextWrapping.Wrap,
							FontSize = size,
							FontWeight = FontWeight.Bold,
							Foreground = new SolidColorBrush(fg),
							Margin = new Thickness(0, 4, 0, 2),
							Inlines = BuildInlines(h.Inlines, fg, link)
						};
					}
				case MdBlockKind.Paragraph:
					{
						var p = (MdParagraph)block;
						return new TextBlock
						{
							TextWrapping = TextWrapping.Wrap,
							Foreground = new SolidColorBrush(fg),
							Inlines = BuildInlines(p.Inlines, fg, link)
						};
					}
				case MdBlockKind.Code:
					{
						var c = (MdCode)block;
						var tb = new SelectableTextBlock
						{
							Text = c.Code,
							FontFamily = new FontFamily("Consolas, Menlo, monospace"),
							TextWrapping = TextWrapping.Wrap,
							Foreground = new SolidColorBrush(fg)
						};
						return new Border
						{
							Background = new SolidColorBrush(codeBg),
							BorderBrush = new SolidColorBrush(border),
							BorderThickness = new Thickness(1),
							CornerRadius = new CornerRadius(4),
							Padding = new Thickness(10),
							Margin = new Thickness(0, 2),
							Child = tb
						};
					}
				case MdBlockKind.List:
					{
						var l = (MdList)block;
						var panel = new StackPanel { Margin = new Thickness(0, 2) };
						for (int idx = 0; idx < l.Items.Count; idx++)
						{
							string bullet = l.Ordered ? $"{idx + 1}." : "•";
						var item = new TextBlock
						{
							TextWrapping = TextWrapping.Wrap,
							Foreground = new SolidColorBrush(fg),
							Margin = new Thickness(0, 1)
						};
						item.Inlines.Add(new Run { Text = bullet + "  " });
						foreach (Inline il in BuildInlines(l.Items[idx], fg, link))
						{
							item.Inlines.Add(il);
						}
						panel.Children.Add(item);
						}
						return panel;
					}
				case MdBlockKind.Quote:
					{
						var q = (MdQuote)block;
						var tb = new TextBlock
						{
							TextWrapping = TextWrapping.Wrap,
							Foreground = new SolidColorBrush(fg),
							Inlines = BuildInlines(q.Inlines, fg, link)
						};
						return new Border
						{
							BorderBrush = new SolidColorBrush(border),
							BorderThickness = new Thickness(4, 0, 0, 0),
							Padding = new Thickness(12, 2, 2, 2),
							Margin = new Thickness(0, 2),
							Child = tb
						};
					}
				case MdBlockKind.Table:
					{
						var t = (MdTable)block;
						int cols = t.Header.Count;
						int rows = t.Rows.Count + 1;
						var grid = new Grid { Margin = new Thickness(0, 4) };
						for (int c = 0; c < cols; c++)
						{
							grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
						}
						for (int r = 0; r < rows; r++)
						{
							grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
						}
						void AddCell(List<MdInline> inlines, int r, int c, bool header)
						{
							var cell = new TextBlock
							{
								TextWrapping = TextWrapping.Wrap,
								Foreground = new SolidColorBrush(fg),
								FontWeight = header ? FontWeight.Bold : FontWeight.Normal,
								Padding = new Thickness(8, 4),
								Inlines = BuildInlines(inlines, fg, link)
							};
							if (header)
							{
								cell.Background = new SolidColorBrush(codeBg);
							}
							Grid.SetRow(cell, r);
							Grid.SetColumn(cell, c);
							grid.Children.Add(cell);
							var sep = new Border
							{
								BorderBrush = new SolidColorBrush(border),
								BorderThickness = new Thickness(0, 0, 1, 1),
								IsHitTestVisible = false
							};
							Grid.SetRow(sep, r);
							Grid.SetColumn(sep, c);
							grid.Children.Add(sep);
						}
						AddCell(t.Header, 0, 0, true);
						for (int c = 1; c < cols; c++)
						{
							AddCell(t.Header.GetRange(c, 1), 0, c, true);
						}
						for (int r = 0; r < t.Rows.Count; r++)
						{
							for (int c = 0; c < cols; c++)
							{
								AddCell(t.Rows[r].GetRange(c, 1), r + 1, c, false);
							}
						}
						return grid;
					}
				case MdBlockKind.Rule:
					return new Separator
					{
						HorizontalAlignment = HorizontalAlignment.Stretch,
						Margin = new Thickness(0, 6)
					};
				default:
					return new TextBlock();
			}
		}

		private InlineCollection BuildInlines(List<MdInline> inlines, Color fg, Color link)
		{
			var result = new InlineCollection();
			if (inlines == null)
			{
				return result;
			}
			foreach (MdInline inline in inlines)
			{
				switch (inline.Kind)
				{
					case MdInlineKind.Text:
						result.Add(new Run { Text = inline.Text });
						break;
					case MdInlineKind.Code:
						result.Add(new Run
						{
							Text = inline.Text,
							FontFamily = new FontFamily("Consolas, Menlo, monospace")
						});
						break;
					case MdInlineKind.Bold:
						var bold = new Bold();
						foreach (Inline child in BuildInlines(inline.Children, fg, link))
						{
							bold.Inlines.Add(child);
						}
						result.Add(bold);
						break;
					case MdInlineKind.Italic:
						var italic = new Italic();
						foreach (Inline child in BuildInlines(inline.Children, fg, link))
						{
							italic.Inlines.Add(child);
						}
						result.Add(italic);
						break;
					case MdInlineKind.Link:
						var hyperlink = new Hyperlink
						{
							Foreground = new SolidColorBrush(link),
							NavigateUri = TryCreateUri(inline.Url)
						};
						hyperlink.Inlines.Add(new Run { Text = inline.Text });
						hyperlink.Click += (_, _) => LinkClicked?.Invoke(inline.Url);
						result.Add(hyperlink);
						break;
				}
			}
			return result;
		}

		private static Uri TryCreateUri(string url)
		{
			if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
			{
				return uri;
			}
			if (Uri.TryCreate(url, UriKind.Relative, out uri))
			{
				return uri;
			}
			return null;
		}

		#endregion
	}
}
