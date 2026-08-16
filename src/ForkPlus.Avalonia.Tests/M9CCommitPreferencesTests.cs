using System;
using System.IO;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Avalonia.Preferences;
using Xunit;

namespace ForkPlus.Avalonia.Tests;

/// <summary>M9-C：Commit Message 偏好页（length/spell/page guide/regex）</summary>
[Collection(nameof(AvaloniaHeadlessCollection))]
public class M9CCommitPreferencesTests
{
    [AvaloniaFact]
    public void M9C_CommitPreferences_DefaultsAndBindings()
    {
        var uc = new CommitPreferencesUserControl();
        var window = new Window { Content = uc, Width = 540, Height = 280 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var vm = uc.ViewModel;

        // 数据流
        Assert.Equal(4, vm.CommitSubjectLowLimit);
        Assert.Equal(50, vm.CommitSubjectHighLimit);
        Assert.Equal("System", vm.SpellChecking);
        Assert.Equal(72, vm.PageGuideColumn);
        Assert.False(string.IsNullOrEmpty(vm.CommitMessageRegex));

        // 可视化
        var tbs = uc.GetVisualDescendants().OfType<TextBox>().ToList();
        Assert.Equal(4, tbs.Count);
        var cbs = uc.GetVisualDescendants().OfType<ComboBox>().ToList();
        Assert.Single(cbs);

        // 操作语义
        vm.SpellChecking = "English";
        Assert.Equal("English", vm.SpellChecking);

        // 截图
        const int W = 540, H = 280;
        window.Width = W; window.Height = H;
        window.InvalidateMeasure(); window.Measure(new Size(W, H));
        window.Arrange(new Rect(0, 0, W, H)); window.UpdateLayout();
        for (int i = 0; i < 6; i++) Dispatcher.UIThread.RunJobs();
        Thread.Sleep(120);
        var rtb = new RenderTargetBitmap(new PixelSize(W, H), new Vector(96, 96));
        rtb.Render(window);
        string outFile = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "verification", "avalonia", "m9-prefs", "avalonia-m9c-prefs-commit.png"));
        Directory.CreateDirectory(Path.GetDirectoryName(outFile)!);
        using (var fs = File.Create(outFile)) rtb.Save(fs, new PngBitmapEncoderOptions());
        Assert.True(new FileInfo(outFile).Length > 1_500);
        File.WriteAllText(Path.ChangeExtension(outFile, ".txt"),
            "M9-C: Commit preferences (length indicator 4/50, spell System, page guide col 72, regex 默认 Conventional Commits)\n");
    }
}
