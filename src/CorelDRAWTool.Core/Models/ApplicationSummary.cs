namespace CorelDRAWTool.Core.Models
{
    /// <summary>
    /// CorelDRAW 应用本身的版本与安装路径信息。
    /// </summary>
    public sealed class ApplicationSummary
    {
        public ApplicationSummary(
            string version,
            int versionMajor,
            int versionMinor,
            string programPath)
        {
            Version = version ?? string.Empty;
            VersionMajor = versionMajor;
            VersionMinor = versionMinor;
            ProgramPath = programPath ?? string.Empty;
        }

        public string Version { get; }

        public int VersionMajor { get; }

        public int VersionMinor { get; }

        public string ProgramPath { get; }

        public string DisplayVersion
        {
            get
            {
                return string.IsNullOrWhiteSpace(Version)
                    ? string.Format("{0}.{1}", VersionMajor, VersionMinor)
                    : Version;
            }
        }
    }
}
