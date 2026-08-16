using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Avalonia.Dialogs;
using Xunit;

namespace ForkPlus.Avalonia.Tests;

/// <summary>
/// M12 阶段：含 ComboBox / ItemsControl 的对话框。
/// 覆盖：ResetBranch（ComboBox + 颜色椭圆 + ResetType 枚举）、
///       MergeBranch（ComboBox + ItemsSource + DataTemplate 复合显示）。
/// 验证：ViewModel 数据传播、ComboBox 选中项→结果回传、对话框无头渲染截图。
/// </summary>
public class M12ComboBoxDialogsTests
{
    [AvaloniaFact]
    public void M12_ResetBranch_DefaultAndPropagation()
    {
        var win = new ResetBranchWindow();
        win.Show();
        Dispatcher.UIThread.RunJobs();

        // 默认值：MIXED（与 WPF 原版一致）
        Assert.Equal(ResetType.Mixed, win.ViewModel.ResetType);

        // 通过 ViewModel 修改并验证 GetResult 回传
        win.ViewModel.ResetType = ResetType.Soft;
        Assert.Equal(ResetType.Soft, win.GetResult());

        win.ViewModel.ResetType = ResetType.Hard;
        Assert.Equal(ResetType.Hard, win.GetResult());

        win.Close();
    }

    [AvaloniaFact]
    public void M12_ResetBranch_ComboBoxContainsThreeItems()
    {
        var win = new ResetBranchWindow { Width = 590, Height = 260 };
        win.Show();
        Dispatcher.UIThread.RunJobs();

        var combo = win.FindControl<ComboBox>("ResetTypeComboBox");
        // ResetBranchWindow.axaml 中没有为 ComboBox 显式设 x:Name 时，回退到整棵 visual tree 找
        combo ??= win.GetVisualDescendants()
                    .OfType<ComboBox>()
                    .FirstOrDefault(c => c.Items.Count == 3);

        Assert.NotNull(combo);
        Assert.Equal(3, combo!.Items.Count);
        win.Close();
    }

    [AvaloniaFact]
    public void M12_MergeBranch_OptionsPopulated()
    {
        var win = new MergeBranchWindow();
        win.Show();
        Dispatcher.UIThread.RunJobs();

        // 默认 4 个 merge 选项（与 WPF 原版一致：Default / NoFastForward / Squash / NoCommit；WPF 中间用 IsSeparator 分隔，此处简化为连续列表）
        Assert.Equal(4, win.ViewModel.Options.Count);
        Assert.NotNull(win.ViewModel.SelectedOption);
        Assert.Equal("Default", win.ViewModel.SelectedOption!.Title);
        Assert.Equal("--no-ff", win.ViewModel.Options[1].Command);
        Assert.Equal("--squash", win.ViewModel.Options[2].Command);
        Assert.Equal("--no-commit", win.ViewModel.Options[3].Command);
        win.Close();
    }

    [AvaloniaFact]
    public void M12_MergeBranch_SelectionAndResultPropagation()
    {
        var win = new MergeBranchWindow();
        win.Show();
        Dispatcher.UIThread.RunJobs();

        // 选中 Squash
        win.ViewModel.SelectedOption = win.ViewModel.Options[2];
        var r = win.GetResult();
        Assert.NotNull(r);
        Assert.Equal("Squash", r!.Title);
        Assert.Equal("--squash", r.Command);

        // 切到 NoCommit
        win.ViewModel.SelectedOption = win.ViewModel.Options[3];
        Assert.Equal("Don't Commit", win.GetResult()!.Title);

        // 还原 Default
        win.ViewModel.SelectedOption = win.ViewModel.Options[0];
        Assert.Equal("Default", win.GetResult()!.Title);

        win.Close();
    }

    [AvaloniaFact]
    public void M12_AllDialogs_Screenshot()
    {
        var dir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "verification", "avalonia", "m12-combo-dialogs"));
        Directory.CreateDirectory(dir);

        var reset = new ResetBranchWindow { Width = 590, Height = 260 };
        reset.ViewModel.ResetType = ResetType.Hard; // 用 Hard 颜色（红）便于肉眼区分

        var merge = new MergeBranchWindow { Width = 560, Height = 225 };
        merge.ViewModel.SelectedOption = merge.ViewModel.Options[1]; // No Fast-Forward

        var wins = new (Window Win, string Name)[]
        {
            (reset, "avalonia-m12-resetbranch.png"),
            (merge, "avalonia-m12-mergebranch.png"),
        };

        long total = 0;
        foreach (var (win, name) in wins)
        {
            win.Show();
            Dispatcher.UIThread.RunJobs();
            win.Measure(new Size(win.Width, win.Height));
            win.Arrange(new Rect(0, 0, win.Width, win.Height));
            Dispatcher.UIThread.RunJobs();
            var rtb = new RenderTargetBitmap(new PixelSize((int)win.Width, (int)win.Height), new Vector(96, 96));
            rtb.Render(win);
            var file = Path.Combine(dir, name);
            rtb.Save(file);
            Assert.True(File.Exists(file));
            var len = new FileInfo(file).Length;
            Assert.True(len > 1000, $"file too small: {file} = {len}");
            total += len;
            win.Close();
        }
        Assert.True(total > 2000);
    }
}
