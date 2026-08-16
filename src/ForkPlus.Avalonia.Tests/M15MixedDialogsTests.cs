using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ForkPlus.Avalonia.Dialogs;
using Xunit;

namespace ForkPlus.Avalonia.Tests;

/// <summary>
/// M15 阶段：表单 + 选择控件混合的对话框。
/// 覆盖：AddSubmodule（双 TextBox + 嵌套子模块勾选 + IsValid 计算）、
///       GenerateNewSshKey（双 TextBox 表单）、
///       GitFlowStartFeature（前缀 + TextBox + 分支 ComboBox）、
///       LeanBranchingStart（GitPointView + TextBox + 三选一 RadioButton 枚举）。
/// </summary>
public class M15MixedDialogsTests
{
    [AvaloniaFact]
    public void M15_AddSubmodule_ValidationAndResult()
    {
        var win = new AddSubmoduleWindow();
        win.Show();
        Dispatcher.UIThread.RunJobs();

        // 空：IsValid = false，GetResult = null
        Assert.False(win.ViewModel.IsValid);
        Assert.Null(win.GetResult());

        // 只填 URL：仍然 invalid
        win.ViewModel.RepositoryUrl = "https://github.com/foo/bar.git";
        Assert.False(win.ViewModel.IsValid);
        Assert.Null(win.GetResult());

        // 路径也填：valid
        win.ViewModel.LocalPath = "external/bar";
        Assert.True(win.ViewModel.IsValid);
        win.ViewModel.FetchNestedSubmodulesChecked = true;
        var r = win.GetResult();
        Assert.NotNull(r);
        Assert.Equal("https://github.com/foo/bar.git", r!.Value.Url);
        Assert.Equal("external/bar", r.Value.Path);
        Assert.True(r.Value.FetchNested);

        // 提示文本：IsFinalPathHintVisible 切换
        Assert.False(win.ViewModel.IsFinalPathHintVisible);
        win.ViewModel.FinalPathHint = "ext/bar";
        Assert.True(win.ViewModel.IsFinalPathHintVisible);
        win.Close();
    }

    [AvaloniaFact]
    public void M15_GenerateNewSshKey_ValidationAndResult()
    {
        var win = new GenerateNewSshKeyWindow();
        win.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.False(win.ViewModel.IsValid);
        Assert.Null(win.GetResult());

        win.ViewModel.KeyFileName = "fork_ssh_key";
        win.ViewModel.Email = "me@example.com";
        Assert.True(win.ViewModel.IsValid);
        var r = win.GetResult();
        Assert.Equal("fork_ssh_key", r!.Value.KeyFileName);
        Assert.Equal("me@example.com", r.Value.Email);
        win.Close();
    }

    [AvaloniaFact]
    public void M15_GitFlowStartFeature_PrefixAndFullName()
    {
        var win = new GitFlowStartFeatureWindow();
        win.ViewModel.Branches.Add(new BranchItem { BranchName = "develop" });
        win.ViewModel.Branches.Add(new BranchItem { BranchName = "main" });
        win.ViewModel.SelectedBranch = win.ViewModel.Branches[0];
        win.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("feature/", win.ViewModel.Prefix);
        Assert.Equal("develop", win.ViewModel.SelectedBranch!.BranchName);

        // 空 name → null
        Assert.Null(win.GetResult());

        // 设置 name → FullName 自动拼接
        win.ViewModel.Name = "user-profile";
        Assert.Equal("feature/user-profile", win.ViewModel.FullName);

        var r = win.GetResult();
        Assert.NotNull(r);
        Assert.Equal("feature/user-profile", r!.Value.FullName);
        Assert.Equal("develop", r.Value.StartPoint!.BranchName);
        win.Close();
    }

    [AvaloniaFact]
    public void M15_LeanBranchingStart_RadioButtonEnumBinding()
    {
        var win = new LeanBranchingStartWindow();
        win.ViewModel.StartBranchOnText = "main · abc1234";
        win.ViewModel.BranchName = "feature/foo";
        win.Show();
        Dispatcher.UIThread.RunJobs();

        // 默认 DoNotChange
        Assert.Equal(LeanBranchingLocalChanges.DoNotChange, win.ViewModel.LocalChangesOption);

        // 通过 VM 切换枚举
        win.ViewModel.LocalChangesOption = LeanBranchingLocalChanges.StashAndReapply;
        Assert.Equal(LeanBranchingLocalChanges.StashAndReapply, win.ViewModel.LocalChangesOption);

        win.ViewModel.LocalChangesOption = LeanBranchingLocalChanges.Discard;
        Assert.Equal(LeanBranchingLocalChanges.Discard, win.ViewModel.LocalChangesOption);

        var r = win.GetResult();
        Assert.NotNull(r);
        Assert.Equal("feature/foo", r!.Value.BranchName);
        Assert.Equal(LeanBranchingLocalChanges.Discard, r.Value.Option);
        win.Close();
    }

    [AvaloniaFact]
    public void M15_LeanBranchingRadioConverter_RoundTrip()
    {
        // 直接验证 Converter
        var c = LeanBranchingRadioConverter.Instance;

        // Convert: enum → bool
        Assert.True((bool)c.Convert(LeanBranchingLocalChanges.DoNotChange, typeof(bool), "DoNotChange", System.Globalization.CultureInfo.InvariantCulture)!);
        Assert.False((bool)c.Convert(LeanBranchingLocalChanges.DoNotChange, typeof(bool), "Discard", System.Globalization.CultureInfo.InvariantCulture)!);
        Assert.True((bool)c.Convert(LeanBranchingLocalChanges.Discard, typeof(bool), "Discard", System.Globalization.CultureInfo.InvariantCulture)!);

        // ConvertBack: IsChecked=true + param → enum
        Assert.Equal(LeanBranchingLocalChanges.StashAndReapply, c.ConvertBack(true, typeof(LeanBranchingLocalChanges), "StashAndReapply", System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(BindingOperations.DoNothing, c.ConvertBack(false, typeof(LeanBranchingLocalChanges), "StashAndReapply", System.Globalization.CultureInfo.InvariantCulture));
    }

    [AvaloniaFact]
    public void M15_AllDialogs_Screenshot()
    {
        var dir = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "verification", "avalonia", "m15-mixed-dialogs"));
        Directory.CreateDirectory(dir);

        var addSub = new AddSubmoduleWindow { Width = 590, Height = 240 };
        addSub.ViewModel.RepositoryUrl = "https://github.com/foo/bar.git";
        addSub.ViewModel.LocalPath = "external/bar";
        addSub.ViewModel.FinalPathHint = "ext/bar";
        addSub.ViewModel.FetchNestedSubmodulesChecked = true;

        var ssh = new GenerateNewSshKeyWindow { Width = 490, Height = 200 };
        ssh.ViewModel.KeyFileName = "fork_ssh_key";
        ssh.ViewModel.Email = "me@example.com";

        var flow = new GitFlowStartFeatureWindow { Width = 520, Height = 220 };
        flow.ViewModel.Name = "user-profile";
        flow.ViewModel.Branches.Add(new BranchItem { BranchName = "develop" });
        flow.ViewModel.Branches.Add(new BranchItem { BranchName = "main" });
        flow.ViewModel.SelectedBranch = flow.ViewModel.Branches[0];

        var lean = new LeanBranchingStartWindow { Width = 610, Height = 280 };
        lean.ViewModel.StartBranchOnText = "main · abc1234";
        lean.ViewModel.BranchName = "feature/foo";
        lean.ViewModel.LocalChangesOption = LeanBranchingLocalChanges.StashAndReapply;

        var wins = new (Window Win, string Name)[]
        {
            (addSub, "avalonia-m15-addsubmodule.png"),
            (ssh, "avalonia-m15-generatesshkey.png"),
            (flow, "avalonia-m15-gitflowstartfeature.png"),
            (lean, "avalonia-m15-leanbranchingstart.png"),
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
        Assert.True(total > 4000);
    }
}
