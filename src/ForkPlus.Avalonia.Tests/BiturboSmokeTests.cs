using ForkPlus.Biturbo;
using Xunit;

namespace ForkPlus.Avalonia.Tests
{
    /// <summary>
    /// M0 验收：biturbo 原生库三平台接线。
    /// 调用 Bt.bt_oid_from_str 会触发 Bt 的静态构造（注册 DllImportResolver，按 OS 重定向到
    /// biturbo.dll / libbiturbo.so / libbiturbo.dylib），并真正 load 原生库。
    /// 若原生库缺失或平台不匹配，会抛 DllNotFoundException / 解析失败，本测试即失败。
    /// </summary>
    public class BiturboSmokeTests
    {
        [Fact]
        public void Biturbo_NativeLib_Loads_And_ParsesOid()
        {
            // 40 字符十六进制 SHA（任意合法值）
            var sha = System.Text.Encoding.UTF8.GetBytes("0123456789abcdef0123456789abcdef01234567");
            BtOid oid = default;
            BtResult result = Bt.bt_oid_from_str(sha, ref oid);

            // Ok 表示原生库已加载且 P/Invoke 成功执行
            Assert.Equal(BtResult.Ok, result);
        }
    }
}
