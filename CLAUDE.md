# CorelDRAWTool — 项目说明（给后续会话）

## 一句话

CorelDRAW 2025 内部效率工具：极薄的 VBA/GMS 入口 + 进程外 .NET Framework 4.8 WPF Host，通过 COM 操作当前正在运行的 CorelDRAW。

## 当前状态

- 解决方案骨架已建立，Release 构建通过，16 个单元测试通过。
- COM 附加已在本机验证（2026-11-04）：能附加到正常启动的 CorelDRAW 并读取活动文档信息。
- GMS 宏 → Host → COM 端到端链路已通过（`scripts\e2e-test.ps1`、`scripts\e2e-batch-test.ps1`）。
- 已实现：GMS 启动器源码与自动构建、COM 附加层、文档信息视图、文件夹批量导出 PDF（后台 STA 线程）、部署与验证脚本。
- 未实现：GMS 菜单/工具栏自动注册、其他批量能力（转换/重命名/预检）、印前/绘图/面板功能、日志、跨版本兼容。

## 环境（本机，2026-11-04 确认）

- CorelDRAW Technical Suite 2025，`C:\Program Files\Corel\CorelDRAW Technical Suite\26`
- 主程序版本 `26.0.0.101`（64 位）
- COM ProgID：`CorelDRAW.Application`（CurVer 指向 `CorelDRAW.Application.26`）
- 互操作程序集：`...\Programs64\Assemblies\Corel.Interop.VGCore.dll`（v26.0.0.101，.NET Framework 4.0 运行时）
- 类型库：`...\Programs64\TypeLibs\CorelDRAW.tlb`
- GMS 目录：`%APPDATA%\Corel\CorelDRAW Technical Suite 2025\Draw\GMS`
- VBA 7.1、VSTA 17.0 已安装；.NET Framework 4.8.1、VS 2022、.NET SDK 8/10 已安装

详细记录见 `docs/environment.md`。

## 架构边界（必须遵守）

- `src/CorelDRAWTool.Core`：纯业务模型与算法，**不得**引用 Corel 互操作。
- `src/CorelDRAWTool.Corel`：**唯一**允许使用 `Corel.Interop.VGCore` 的项目，暴露 `ICorelDrawSession`。
- `src/CorelDRAWTool.Host`：WPF UI 与功能编排，只通过 `ICorelDrawSession` 访问 CorelDRAW，**不得**直接引用互操作类型。
- `shell/vba`：只负责启动 Host，**不得**写业务逻辑。
- 新增功能先登记到 `Core/Features/FeatureCatalog.cs`，再在 Core 写逻辑、Corel 加适配方法、Host 加视图。

### COM 附加注意

正常启动的 CorelDRAW 不在 ROT 中，`Marshal.GetActiveObject` 会返回 `MK_E_UNAVAILABLE`；`CorelDrawSession.Attach` 会回退到 ProgID COM 激活（`Activator.CreateInstance`），该方式会转发到已运行实例，不会新开进程。不要把这层回退删掉。

### 批量导出实现

- Core：`Batch/BatchExportPlanner` 只做纯计划（枚举 `.cdr`、计算输出路径、处理覆盖/跳过），不访问 CorelDRAW。
- Corel：`CorelDrawSession.ExportDocumentToPdf` 使用 `OpenDocumentAsCopy` + `PublishToPDF` + `Close`，不修改源文件。
- Host：`BatchExportViewModel` 在专用 STA 工作线程上创建独立 `CorelDrawSession`，通过 `Progress<T>` 把进度回报到 UI；不要改为在 UI 线程或 MTA 后台线程直接调用 COM。
- 取消只在文件之间生效，当前正在导出的文件会完成。

## 构建 / 测试 / 部署

```powershell
dotnet build CorelDRAWTool.sln -c Release
dotnet test CorelDRAWTool.sln -c Release
powershell -ExecutionPolicy Bypass -File scripts\build.ps1
powershell -ExecutionPolicy Bypass -File scripts\build-shell.ps1
powershell -ExecutionPolicy Bypass -File scripts\deploy.ps1
powershell -ExecutionPolicy Bypass -File scripts\integration-test.ps1
powershell -ExecutionPolicy Bypass -File scripts\ui-smoke-test.ps1
powershell -ExecutionPolicy Bypass -File scripts\e2e-test.ps1
powershell -ExecutionPolicy Bypass -File scripts\e2e-batch-test.ps1
powershell -ExecutionPolicy Bypass -File scripts\ui-batch-test.ps1
```

- `build-shell.ps1` 需要 CorelDRAW 正在运行；它用 `GMSManager` + VBE 生成 `shell\build\CorelDRAWTool.gms`，宏路径为 `CorelDRAWToolShell.ShowCorelDRAWTool` 和 `CorelDRAWToolShell.ShowBatchExport`。
- `deploy.ps1 -BuildShell` 可先构建 GMS 再部署。
- `e2e-test.ps1` / `e2e-batch-test.ps1` 会加载已部署的 GMS、运行对应宏、启动 Host 并用 UI Automation 验证窗口，然后关闭 Host。
- `ui-batch-test.ps1` 会自动创建测试 `.cdr`，通过 UI Automation 驱动批量导出并校验生成的 PDF。

- 目标框架：`net48`（WPF Host 使用 `net48` + `UseWPF`）。
- 通过 `Directory.Build.props` 的 `CorelDrawDir` 定位互操作程序集；可用 `-p:CorelDrawDir=...` 或 `CORELDRAW_DIR` 覆盖。
- 部署路径：Host 到 `%APPDATA%\Corel\CorelDRAW Technical Suite 2025\Draw\GMS\CorelDRAWTool\app`；GMS 到同一 GMS 目录。GMS 宏从自身路径定位 Host。
- **不要**把 Host 部署到 `%LOCALAPPDATA%\CorelDRAWTool`：Claude Code 桌面会话的 sandbox 会重定向该路径，CorelDRAW 进程看不到。
- `scripts/smoke-test.ps1` 做只读 COM 验证，不修改文档。

## 约定

- UI 文案使用简体中文；代码标识符用英文。
- 不在 VBA 中写业务逻辑；功能迭代应只改 .NET。
- 所有 CorelDRAW COM 调用必须在 STA 线程：文档信息在 WPF UI 线程；批量导出在专用 STA 工作线程并使用独立 session。不要从 MTA 后台线程直接调用 COM。
- 不调用 `Application.Quit()`，不关闭用户的 CorelDRAW。
- 单元测试覆盖 Core 层；Corel 层只做薄适配，集成行为用 `scripts/smoke-test.ps1`、`scripts/integration-test.ps1`、`scripts/e2e*.ps1` 验证。
- `.ps1` 与 `.bas` 保持 ASCII-only：Windows PowerShell 5.1 会把无 BOM 的 UTF-8 脚本按 ANSI 解析，中文注释曾导致脚本执行异常；VBA 导入也有编码风险。
- 文件编码 UTF-8；C# 源文件可以使用中文。

## 已知限制

- 仅验证 CorelDRAW 2025（v26）。
- 浮窗不是原生 Docker，不能停靠在 CorelDRAW 窗口内部。
- GMS 自动构建依赖 CorelDRAW 正在运行且 VBA 项目对象模型可访问；手工导入作为兜底，见 `shell/README.md`。
- 批量导出 PDF 使用 CorelDRAW 默认 PDF 设置，暂未提供导出预设（页面范围、压缩、字体嵌入等）。
- 取消只在文件之间生效，当前文件会完成导出；复杂文档的 PDF 可能很大、耗时较长。
- 无日志系统，错误显示在 Host 状态栏。

## 下一步（建议顺序）

1. 扩展批量导出（PNG/TIFF、页面范围、PDF 预设）或实现批量转换/重命名/预检。
2. 自动注册 GMS 菜单/工具栏按钮（当前需从“工具 > 宏”手动运行，或由用户自行分配快捷键）。
3. 增加日志（本地文件）与全局异常处理。
4. 视需要引入设置持久化（JSON 或注册表 HKCU）。
