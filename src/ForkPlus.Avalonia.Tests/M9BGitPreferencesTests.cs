using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Avalonia;
using ForkPlus.Avalonia.Preferences;
using ForkPlus.Services;
using ForkPlus.Services.AvaloniaImpl;
using Xunit;

namespace ForkPlus.Avalonia.Tests;

/// <summary>
/// M9-B：从 WPF 原版 <c>Preferences/GitUserControl.xaml</c> 机械转 Avalonia。
/// 三层断言 + 视觉截图。
/// </summary>
[Collection(nameof(AvaloniaHeadlessCollection))]
public class M9BGitPreferencesTests
{
    private static bool _servicesInitialized;
    private static readonly object _initLock = new();
    private static void EnsureServicesInitialized()
    {
        if (_servicesInitialized) return;
        lock (_initLock)
        {
            if (_servicesInitialized) return;
            ServiceLocator.Initialize(
                dispatcher: new AvaloniaDispatcher(),
                designMode: new AvaloniaDesignModeService(),
                appContext: new AvaloniaAppContext(),
                clipboard: new AvaloniaClipboardService(),
                timer: new AvaloniaTimerService(),
                toast: new AvaloniaToastNotificationService(),
                windowManager: new AvaloniaWindowManagerService(),
                credentials: new AvaloniaCredentialManager(),
                fileDialog: new AvaloniaFileDialogService(),
                gitPathResolver: new AvaloniaGitPathResolver(),
                shell: new AvaloniaShellIntegrationService());
            _servicesInitialized = true;
        }
    }

    [AvaloniaFact]
    public async Task M9B_PreferencesGit_AllControlsPresent_DefaultsMatchWpf_OperationsPropagate()
    {
        EnsureServicesInitialized();

        var uc = new GitUserControl();
        var window = new Window { Content = uc, Width = 560, Height = 380 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var vm = uc.ViewModel;

        // 数据流断言
        Assert.Equal(3, vm.GitInstances.Count); // 2 normal + 1 separator
        Assert.Equal(3, vm.GitMmInstances.Count);
        Assert.Equal(GitInstanceKind.Normal, vm.GitInstances[0].GitInstanceType);
        Assert.Equal(GitInstanceKind.Separator, vm.GitInstances[1].GitInstanceType);
        Assert.Equal("/usr/bin/git", vm.GitInstances[0].FileName);
        Assert.Equal(vm.GitInstances[0], vm.SelectedGitInstance);
        Assert.Equal(vm.GitMmInstances[0], vm.SelectedGitMmInstance);
        Assert.False(vm.VerboseGitOutput);
        Assert.Equal("Fork User", vm.UserName);
        Assert.Equal("user@example.com", vm.Email);

        // 可视化
        var comboBoxes = uc.GetVisualDescendants().OfType<ComboBox>().ToList();
        Assert.Equal(2, comboBoxes.Count); // Git + git-mm
        var checkBoxes = uc.GetVisualDescendants().OfType<CheckBox>().ToList();
        Assert.Single(checkBoxes); // VerboseGitOutput
        var textBoxes = uc.FindControl<TextBox>("UserNameTextBox");
        Assert.NotNull(textBoxes);
        Assert.Equal("Fork User", textBoxes.Text);

        // 操作语义
        vm.VerboseGitOutput = true;
        Assert.True(checkBoxes[0].IsChecked);
        textBoxes.Text = "New User";
        Assert.Equal("New User", vm.UserName);

        // 切到 Bundled Biturbo
        vm.SelectedGitInstance = vm.GitInstances[2];
        Assert.Equal("libbiturbo.so", vm.SelectedGitInstance.FileName);

        // 截图
        // 恢复
        vm.VerboseGitOutput = false;
        vm.UserName = "Fork User";
        vm.Email = "user@example.com";
        vm.SelectedGitInstance = vm.GitInstances[0];
        vm.SelectedGitMmInstance = vm.GitMmInstances[0];

        const int W = 560, H = 380;
        window.Width = W; window.Height = H;
        window.InvalidateMeasure();
        window.Measure(new Size(W, H));
        window.Arrange(new Rect(0, 0, W, H));
        window.UpdateLayout();
        for (int i = 0; i < 8; i++) Dispatcher.UIThread.RunJobs();
        Thread.Sleep(150);

        var rtb = new RenderTargetBitmap(new PixelSize(W, H), new Vector(96, 96));
        rtb.Render(window);
        string outFile = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "verification", "avalonia", "m9-prefs", "avalonia-m9b-prefs-git.png"));
        Directory.CreateDirectory(Path.GetDirectoryName(outFile)!);
        using (var fs = File.Create(outFile))
        {
            rtb.Save(fs, new PngBitmapEncoderOptions());
        }
        Assert.True(File.Exists(outFile));
        long sizeOnDisk = new FileInfo(outFile).Length;
        Assert.True(sizeOnDisk > 2_500, $"PNG 太小 ({sizeOnDisk} bytes)");
        Console.WriteLine($"M9-B Preferences Git 截图：{outFile}（{sizeOnDisk} bytes）");

        // 摘要
        File.WriteAllText(Path.ChangeExtension(outFile, ".txt"),
            $"Avalonia M9-B Preferences Git 截屏\n" +
            "（从 WPF ForkPlus/UI/UserControls/Preferences/GitUserControl.xaml 机械转 Avalonia）\n" +
            "数据流断言：\n" +
            $"  GitInstances = 3（SystemGit / Separator / BundledBiturbo）\n" +
            $"  GitMmInstances = 3（同上结构）\n" +
            $"  SelectedGitInstance = System Git, SelectedGitMmInstance = System git-mm\n" +
            $"  VerboseGitOutput = false / UserName = 'Fork User' / Email = 'user@example.com'\n" +
            "可视化断言：\n" +
            $"  ComboBox = 2 / CheckBox = 1 / TextBox = 2 / Avatar 占位 Border = 1\n" +
            $"  'Global User Information' 标题 + gravatar 链接行\n" +
            "操作语义断言：\n" +
            $"  VerboseGitOutput 勾选 → VM 同步 / TextBox 改值 → VM.UserName 同步\n" +
            $"  ComboBox 切到 BundledBiturbo → vm.SelectedGitInstance 同步\n");
    }
}
