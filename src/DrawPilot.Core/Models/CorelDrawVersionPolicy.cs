namespace DrawPilot.Core.Models
{
    /// <summary>
    /// CorelDRAW 版本支持策略。
    /// 主版本号参考：2022 = 24，2023 = 24.5，2024 = 25，2025 = 26，2026 = 27。
    /// 不设上限，新版本默认允许；低于下限时只提示，不阻断。
    /// </summary>
    public static class CorelDrawVersionPolicy
    {
        public const int MinimumMajorVersion = 24;

        public const string MinimumProductYear = "2022";

        public const string SupportedRangeLabel = "CorelDRAW " + MinimumProductYear + " 及以上";

        public static bool IsSupported(int majorVersion)
        {
            return majorVersion >= MinimumMajorVersion;
        }

        public static string DescribeUnsupported(int majorVersion, int minorVersion)
        {
            return string.Format(
                "当前 CorelDRAW 版本为 {0}.{1}，低于支持范围（{2}），部分功能可能不可用。",
                majorVersion,
                minorVersion,
                SupportedRangeLabel);
        }
    }
}
