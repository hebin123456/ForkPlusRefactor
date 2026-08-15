using Xunit;

namespace ForkPlus.Avalonia.Tests;

/// <summary>
/// Avalonia Headless 测试共享的 xUnit Collection：
/// 同一 Collection 内测试**串行**执行，规避 [AvaloniaFact] Cleanup 阶段
/// 的 headless dispatcher 重建被并发触发的 "different thread owns it" 错误
/// （详见 <c>AssemblyInfo.cs</c> 的注释）。
/// </summary>
[CollectionDefinition(nameof(AvaloniaHeadlessCollection), DisableParallelization = true)]
public class AvaloniaHeadlessCollection
{
}
