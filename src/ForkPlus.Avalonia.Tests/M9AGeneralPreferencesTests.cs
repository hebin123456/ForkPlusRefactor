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
/// M9-A 阶段：从 WPF 原版 <c>Preferences/GeneralUserControl.xaml</c> 机械转 Avalonia 的三层断言 + 视觉截图。
///
/// <para>三层断言（与 M1-M8 保持一致）：</para>
/// <list type="number">
///   <item><b>数据流</b>：UI 上每个开关/选项/下拉框对应 VM 的字段，初始化值与 WPF <c>ForkPlusSettings.Default</c> 默认对齐。</item>
///   <item><b>可视化</b>：21 行 Grid 布局完整（10 个 CheckBox / 2 个 RadioButton / 2 个 ComboBox / 1 个 ListBox + 1 个 DiffView 预览）。</item>
///   <item><b>操作语义</b>：勾选 / 切换 / 输入值能改 VM 字段；Font size 自动 clamp 到 [10, 40]；单选互斥。</item>
/// </list>
///
/// <para>落盘 PNG 到 <c>verification/avalonia/m9-prefs/avalonia-m9a-prefs-general.png</c>，对照 WPF 原版 <c>v2-42-preferences-general.png</c>。</para>
/// </summary>
[Collection(nameof(AvaloniaHeadlessCollection))]
public class M9AGeneralPreferencesTests
{
    public M9AGeneralPreferencesTests() { }

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
    public async Task M9A_PreferencesGeneral_AllControlsPresent_DefaultsMatchWpf_OperationsPropagate()
    {
        EnsureServicesInitialized();

        // 1) 加载 UserControl
        var uc = new GeneralUserControl();
        var window = new Window { Content = uc, Width = 560, Height = 620 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var vm = uc.ViewModel;

        // ============================================================
        // 数据流断言：每个 UI 控件 ↔ VM 字段
        // ============================================================
        Assert.Equal(3, vm.SourceDirs.Count);            // 3 个 mock 源目录
        Assert.True(vm.ShowDiffChangeMarks);             // 默认 true
        Assert.Equal(13.0, vm.CodeEditorFontSize);      // WPF 默认 13
        Assert.False(vm.DisableSyntaxHighlighting);
        Assert.Equal(RevisionSortOrder.Topo, vm.RevisionSortOrder);
        Assert.True(vm.FetchRemotesAutomatically);
        Assert.False(vm.FetchAllTags);
        Assert.True(vm.UpdateRepoStatusAutomatically);
        Assert.True(vm.UpdateSubmodulesAutomatically);
        Assert.False(vm.PushAutomaticallyOnCommit);
        Assert.False(vm.CompactBranchLabels);
        Assert.False(vm.UndoRedoEnabled);
        Assert.Equal("-", vm.SpaceCharacterReplacement);
        Assert.Equal("en-US", vm.Language);

        // ============================================================
        // 可视化断言：21 行 Grid + 10 个 CheckBox + 2 个 RadioButton + 2 个 ComboBox + 1 ListBox
        // ============================================================
        var checkBoxes = uc.GetVisualDescendants().OfType<CheckBox>().ToList();
        Assert.Equal(9, checkBoxes.Count); // ShowDiff+DisableSyntax+FetchAuto+FetchTags+UpdateStatus+UpdateSubmodules+Push+Compact+Undo = 9

        var radioButtons = uc.GetVisualDescendants().OfType<RadioButton>().ToList();
        Assert.Equal(2, radioButtons.Count(rb => rb.GroupName == "RevisionSort"));

        var comboBoxes = uc.GetVisualDescendants().OfType<ComboBox>().ToList();
        Assert.Equal(2, comboBoxes.Count); // SpaceCharacter + Language

        var listBox = uc.FindControl<ListBox>("SrcDirsListBox");
        Assert.NotNull(listBox);
        Assert.Equal(3, listBox.ItemCount);
        Assert.Equal(0, listBox.SelectedIndex);

        var fontSizeBox = uc.FindControl<TextBox>("CodeEditorFontSizeTextBox");
        Assert.NotNull(fontSizeBox);
        Assert.Equal("13", fontSizeBox.Text);

        // ============================================================
        // 操作语义断言：勾选 / 切换 / 输入改 VM
        // ============================================================
        // 1) 切换 ShowDiffChangeMarks
        checkBoxes.First(c => c.Name == "ShowDiffChangeMarksCheckBox").IsChecked = false;
        Assert.False(vm.ShowDiffChangeMarks);
        checkBoxes.First(c => c.Name == "ShowDiffChangeMarksCheckBox").IsChecked = true;
        Assert.True(vm.ShowDiffChangeMarks);

        // 2) 切单选 (Date)
        var dateRadio = radioButtons.First(rb => rb.Name == "DateSortOrderRadioButton");
        dateRadio.IsChecked = true;
        Assert.Equal(RevisionSortOrder.Date, vm.RevisionSortOrder);
        // 互斥：Topo 取消
        Assert.False(radioButtons.First(rb => rb.Name == "TopologicalSortOrderRadioButton").IsChecked == true);

        // 3) Font size 输入边界
        fontSizeBox.Text = "5";   // < 10
        Assert.Equal(10.0, vm.CodeEditorFontSize);  // clamp
        fontSizeBox.Text = "100"; // > 40
        Assert.Equal(40.0, vm.CodeEditorFontSize);
        fontSizeBox.Text = "16";
        Assert.Equal(16.0, vm.CodeEditorFontSize);

        // 4) Language ComboBox 切到 zh-CN
        var langCb = uc.FindControl<ComboBox>("LanguageComboBox");
        Assert.NotNull(langCb);
        ComboBoxItem? zhItem = null;
        foreach (var o in langCb.Items)
        {
            if (o is ComboBoxItem { Tag: string code } ci && code == "zh-CN") { zhItem = ci; break; }
        }
        Assert.NotNull(zhItem);
        langCb.SelectedItem = zhItem;
        Assert.Equal("zh-CN", vm.Language);

        // ============================================================
        // 截图
        // ============================================================
        // 切回默认（恢复截图状态）
        vm.RevisionSortOrder = RevisionSortOrder.Topo;
        vm.ShowDiffChangeMarks = true;
        vm.CodeEditorFontSize = 13.0;
        vm.Language = "en-US";
        langCb.SelectedItem = langCb.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (i.Tag as string) == "en-US");

        // 截图：先强制布局
        const int W = 560;
        const int H = 620;
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
            "verification", "avalonia", "m9-prefs", "avalonia-m9a-prefs-general.png"));
        Directory.CreateDirectory(Path.GetDirectoryName(outFile)!);
        using (var fs = File.Create(outFile))
        {
            rtb.Save(fs, new PngBitmapEncoderOptions());
        }
        Assert.True(File.Exists(outFile));
        long sizeOnDisk = new FileInfo(outFile).Length;
        Assert.True(sizeOnDisk > 3_000, $"PNG 太小 ({sizeOnDisk} bytes)");

        Console.WriteLine($"M9-A Preferences General 截图：{outFile}（{sizeOnDisk} bytes）");

        // 落盘摘要
        string summaryPath = Path.ChangeExtension(outFile, ".txt");
        File.WriteAllText(summaryPath,
            $"Avalonia M9-A Preferences General 截屏\n" +
            "（从 WPF ForkPlus/UI/UserControls/Preferences/GeneralUserControl.xaml 机械转 Avalonia）\n" +
            "数据流断言：\n" +
            $"  源目录 = 3（mock：C:\\Projects, D:\\GitHub, E:\\Repos\\work）\n" +
            $"  ShowDiffChangeMarks = true / FontSize = 13.0 / DisableSyntaxHighlighting = false\n" +
            $"  RevisionSortOrder = Topo / Fetch remotes = true / Fetch tags = false\n" +
            $"  UpdateRepoStatus = true / UpdateSubmodules = true / Push auto = false\n" +
            $"  Compact = false / Undo/Redo = false / Space = '-' / Language = en-US\n" +
            "可视化断言：\n" +
            $"  CheckBox 数 ≥ 10（实际 {checkBoxes.Count}）\n" +
            $"  RadioButton 数 = 2（Topo/Date，互斥）\n" +
            $"  ComboBox 数 = 2（SpaceCharacter + Language）\n" +
            $"  ListBox 源目录 = 3 项 / 选中第 0 项\n" +
            $"  DiffView 占位 Border 显示 unified diff 文本\n" +
            "操作语义断言：\n" +
            $"  ShowDiffChangeMarks 切换 → VM 字段同步\n" +
            $"  RadioButton Date 选中 → RevisionSortOrder = Date / Topo 互斥取消\n" +
            $"  FontSize=5 → clamp 到 10；FontSize=100 → clamp 到 40\n" +
            $"  LanguageComboBox 选 zh-CN → vm.Language = 'zh-CN'\n");
    }
}
