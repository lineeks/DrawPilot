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

## 支持范围

| CorelDRAW | 主版本 | 状态 |
|---|---|---|
| 2022 | 24 | 未实测 |
| 2023 | 24.5 | 未实测 |
| 2024 | 25 | 未实测（本机只有残留用户目录，程序未安装） |
| 2025 | 26 | 已实测 |
| 2026 | 27 | 未实测 |

构建和部署按“2022 及以上”设计；运行时低于主版本 24 只提示，不阻断。旧版本问题用 `scripts\compat-report.ps1` 收集信息。

## 说明

- CorelDRAW 的 `Programs64\Addons` 目录中是 Corel 自家的 native addon，`.addon` 文件是零字节标记文件；没有找到公开的第三方 addon SDK，因此本项目不采用 native addon。
- 本项目的 .NET 程序以**进程外**方式通过 COM 附加到已运行的 CorelDRAW，不需要把 DLL 注册到系统，也不需要管理员权限。
- COM 附加要求 Host 与 CorelDRAW 运行在同一个 Windows 用户会话、相同的完整性级别（不要一边以管理员运行、另一边普通运行）。
- 构建时通过 `Directory.Build.props` 中的 `CorelDrawDir` 定位互操作程序集。默认是上面的安装路径；在其他机器上可用 `-p:CorelDrawDir=...` 或环境变量 `CORELDRAW_DIR` 覆盖。
- `scripts\smoke-test.ps1` 用于只读验证 COM 连接和对象模型；不会修改文档。

## 已验证

- `dotnet build DrawPilot.sln -c Release`：通过（0 警告 / 0 错误）。
- `dotnet test`：40 个单元测试通过（批量计划、命名、设置、文档模型、功能清单、版本策略）。
- `scripts\smoke-test.ps1`：只读验证通过，能读取 CorelDRAW 版本、活动文档、页面、图层和选中对象数量。
- `scripts\integration-test.ps1`：适配层 `CorelDrawSession.Attach` 成功附加并读取相同信息。
- `scripts\adapter-batch-test.ps1`：PNG/TIFF 两页逐页导出、PDF 全部页/当前页、空页 warning 均通过；PDF 页数经 `pdfinfo` 验证为 2 / 1。
- `scripts\build-shell.ps1`：自动生成 `shell\build\DrawPilot.gms`，宏路径为 `DrawPilotShell.ShowMain` 和 `DrawPilotShell.ShowBatchExport`。
- `scripts\e2e-test.ps1` / `scripts\e2e-batch-test.ps1`：GMS 宏 → Host → COM 端到端通过，分别打开文档信息和批量导出视图。
- `scripts\ui-batch-test.ps1`：UI Automation 驱动 PDF 批量导出通过，并验证 `settings.json` 与 `data\logs` 日志。
- `scripts\ui-batch-images-test.ps1`：通过设置加载 PNG，驱动 UI 导出两页 PNG 通过。
- `scripts\build.ps1` / `scripts\deploy.ps1 -WhatIf`：能自动识别已安装的 2025，并把 2024 残留用户目录标记为 orphan 跳过。
- 关键发现：正常启动的 CorelDRAW 不在 ROT 中，`GetActiveObject` 返回 `MK_E_UNAVAILABLE`；ProgID COM 激活会转发到已运行实例，不会新开进程。适配层采用“ROT 优先 + COM 激活回退”。
- 关键发现：自动化脚本访问 `GMSManager.Projects` 前需调用 `Application.InitializeVBA()`；否则可能得到 `Projects.Count=0` 并在 `Load` 时返回 `RPC_E_SERVERFAULT`。COM 集合用 `Item(i)` 索引遍历，PowerShell `foreach` 不可靠。

## 部署路径与 sandbox

- Claude Code 桌面会话的 Bash 沙箱会把新创建的 `%LOCALAPPDATA%\DrawPilot` 重定向到 `...\Packages\Claude_...\LocalCache\...`；CorelDRAW 进程看不到这个重定向目录。
- 因此 Host 部署在 CorelDRAW 用户 GMS 目录下：
  `%APPDATA%\Corel\CorelDRAW Technical Suite 2025\Draw\GMS\DrawPilot\app`
- Host 的设置和日志在同级 `data` 目录：`settings.json`、`logs\batch-*.log`；部署脚本只替换 `app`，保留 `data`。
- GMS 宏通过 `Application.GMSManager.Projects("DrawPilot").FullFileName` 取自身路径，再定位同目录的 Host。不要改回 `%LOCALAPPDATA%`。

## 脚本编码

- `.ps1` 与 `.bas` 保持 ASCII-only。
- 本机 Windows PowerShell 5.1 会把无 BOM 的 UTF-8 脚本按 ANSI 解析；中文注释曾导致脚本执行异常。C# 源文件由 Roslyn 按 UTF-8 读取，可以正常使用中文。
