using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(ForkPlus.Avalonia.Tests.SkiaHeadlessTestApp))]

// Avalonia Headless 的 platform instance 在 [AvaloniaFact] 间复用，但 Cleanup 阶段
// 会触发 EnsureIsolatedApplication() 重建一个 headless dispatcher。
// 上一版在 ubuntu CI 上偶发 "different thread owns it"（ServerCompositor 在 ctor 里
// 调 DefaultRenderLoop.Add() 触到 VerifyAccess），根因是 Cleanup 跑在后台线程而
// dispatcher 还活着。
//
// 解决：把所有 [AvaloniaFact] 归到同一 Collection "AvaloniaHeadless"，xUnit 保证
// 同一 Collection 内串行 —— Cleanup 永远在前一个 [AvaloniaFact] 跑完后才启动。
// Collection 跨文件共享，所以 M3 / M4 / MainWindow / MarkdownViewer / AvalonEdit
// 测试类加 [Collection("AvaloniaHeadless")] 即可。
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = false)]
