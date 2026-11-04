# 运行环境记录

本机环境（初始化时确认，2026-11-04）：

| 项目 | 值 |
|---|---|
| 操作系统 | Windows 10 Pro 10.0.19045 |
| CorelDRAW | CorelDRAW Technical Suite 2025 |
| CorelDRAW 版本 | `26.0.0.101`（64 位） |
| 安装目录 | `C:\Program Files\Corel\CorelDRAW Technical Suite\26` |
| 主程序 | `...\Programs64\CorelDRW.exe` |
| COM ProgID | `CorelDRAW.Application`（当前指向 `CorelDRAW.Application.26`） |
| 互操作程序集 | `...\Programs64\Assemblies\Corel.Interop.VGCore.dll`（v26.0.0.101，.NET Framework 4.0 运行时） |
| 类型库 | `...\Programs64\TypeLibs\CorelDRAW.tlb` |
| VBA | Microsoft VBA 7.1 已安装（`C:\Program Files\Common Files\Microsoft Shared\VBA\VBA7.1`） |
| GMS 目录 | `%APPDATA%\Corel\CorelDRAW Technical Suite 2025\Draw\GMS` |
| VSTA | Microsoft Visual Studio Tools for Applications 17.0 已安装 |
| .NET Framework | 4.8.1 |
| 开发工具 | Visual Studio 2022 Community；.NET SDK 8 / 10 |
| 解决方案目标框架 | `net48` |

## 说明

- CorelDRAW 的 `Programs64\Addons` 目录中是 Corel 自家的 native addon，`.addon` 文件是零字节标记文件；没有找到公开的第三方 addon SDK，因此本项目不采用 native addon。
- 本项目的 .NET 程序以**进程外**方式通过 COM 附加到已运行的 CorelDRAW，不需要把 DLL 注册到系统，也不需要管理员权限。
- COM 附加要求 Host 与 CorelDRAW 运行在同一个 Windows 用户会话、相同的完整性级别（不要一边以管理员运行、另一边普通运行）。
- 构建时通过 `Directory.Build.props` 中的 `CorelDrawDir` 定位互操作程序集。默认是上面的安装路径；在其他机器上可用 `-p:CorelDrawDir=...` 或环境变量 `CORELDRAW_DIR` 覆盖。
- `scripts\smoke-test.ps1` 用于只读验证 COM 连接和对象模型；不会修改文档。

## 已验证

- `dotnet build CorelDRAWTool.sln -c Release`：通过（0 警告 / 0 错误）。
- `dotnet test`：16 个单元测试通过（含批量计划器）。
- `scripts\smoke-test.ps1`：只读验证通过，能读取 CorelDRAW 版本、活动文档、页面、图层和选中对象数量。
- `scripts\integration-test.ps1`：适配层 `CorelDrawSession.Attach` 成功附加并读取相同信息。
- `scripts\build-shell.ps1`：自动生成 `shell\build\CorelDRAWTool.gms`，宏路径为 `CorelDRAWToolShell.ShowCorelDRAWTool` 和 `CorelDRAWToolShell.ShowBatchExport`。
- `scripts\e2e-test.ps1` / `scripts\e2e-batch-test.ps1`：GMS 宏 → Host → COM 端到端通过，分别打开文档信息和批量导出视图。
- `scripts\ui-batch-test.ps1`：UI Automation 驱动批量导出通过，生成测试 PDF（测试用 `.cdr` 由脚本自动创建）。
- 关键发现：正常启动的 CorelDRAW 不在 ROT 中，`GetActiveObject` 返回 `MK_E_UNAVAILABLE`；ProgID COM 激活会转发到已运行实例，不会新开进程。适配层采用“ROT 优先 + COM 激活回退”。

## 部署路径与 sandbox

- Claude Code 桌面会话的 Bash 沙箱会把新创建的 `%LOCALAPPDATA%\CorelDRAWTool` 重定向到 `...\Packages\Claude_...\LocalCache\...`；CorelDRAW 进程看不到这个重定向目录。
- 因此 Host 部署在 CorelDRAW 用户 GMS 目录下：
  `%APPDATA%\Corel\CorelDRAW Technical Suite 2025\Draw\GMS\CorelDRAWTool\app`
- GMS 宏通过 `Application.GMSManager.Projects("CorelDRAWTool").FullFileName` 取自身路径，再定位同目录的 Host。不要改回 `%LOCALAPPDATA%`。

## 脚本编码

- `.ps1` 与 `.bas` 保持 ASCII-only。
- 本机 Windows PowerShell 5.1 会把无 BOM 的 UTF-8 脚本按 ANSI 解析；中文注释曾导致脚本执行异常。C# 源文件由 Roslyn 按 UTF-8 读取，可以正常使用中文。
