using System;
using System.IO;

namespace DrawPilot.Host.Services
{
    /// <summary>
    /// 用户数据放在 Host 的 app 目录旁边（data 子目录），部署时不会被覆盖。
    /// </summary>
    public static class AppPaths
    {
        public static string DataDirectory
        {
            get
            {
                return Path.GetFullPath(
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "data"));
            }
        }

        public static string SettingsFile
        {
            get { return Path.Combine(DataDirectory, "settings.json"); }
        }

        public static string LogDirectory
        {
            get { return Path.Combine(DataDirectory, "logs"); }
        }

        public static string CreateLogPath()
        {
            Directory.CreateDirectory(LogDirectory);
            return Path.Combine(
                LogDirectory,
                "batch-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".log");
        }
    }
}
