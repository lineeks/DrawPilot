# DrawPilot

面向内部团队的 CorelDRAW 2025 效率工具。已提供文档信息面板和文件夹批量导出（PDF / PNG / TIFF），后续在同一骨架上扩展批量处理、印前/制版、绘图/排版、面板/对象管理能力。

## 架构

```
GMS 壳 (VBA)  ──启动──►  DrawPilot.Host.exe (WPF, net48)
                              │
                              │ COM 附加（ROT / ProgID 激活）
                              ▼
                         CorelDRAW 2025 (v26)
```

- **GMS 壳**：只负责菜单/按钮入口和启动 Host，不写业务逻辑。
- **Host**：WPF 桌面程序，负责 UI 与功能编排。
- **Corel**：唯一的 COM 适配层，引用 Corel 官方互操作程序集。
- **Core**：纯业务逻辑，不依赖 CorelDRAW，可单元测试。

详细说明见 `docs/architecture.md`，环境记录见 `docs/environment.md`，部署步骤见 `docs/deployment.md`。

## 项目结构

```
src/
  DrawPilot.Core/    纯业务模型、功能清单
  DrawPilot.Corel/   CorelDRAW COM 适配层
  DrawPilot.Host/    WPF 桌面程序
shell/vba/                VBA/GMS 入口源码
tests/
  DrawPilot.Core.Tests/
scripts/                 构建、部署、冒烟/集成/端到端测试
docs/                    架构、环境、部署文档
```

## 快速开始

```powershell
dotnet build DrawPilot.sln -c Release
dotnet test DrawPilot.sln -c Release
powershell -ExecutionPolicy Bypass -File scripts\build-shell.ps1
powershell -ExecutionPolicy Bypass -File scripts\deploy.ps1
```

然后按 `shell\README.md` 安装 GMS 壳，在 CorelDRAW 中运行：

- `DrawPilotShell.ShowMain`：打开 Host，默认显示文档信息；
- `DrawPilotShell.ShowBatchExport`：打开 Host，直接进入批量导出。

## 批量导出

在 Host 左侧选择“批量导出”（或直接运行 `ShowBatchExport` 宏），选择输入文件夹（包含 `.cdr`）和输出文件夹，然后选择：

- 格式：PDF / PNG / TIFF；
- 页面范围：全部页 / 当前页；
- 图片分辨率：150 / 300 / 600 DPI（仅 PNG/TIFF）；
- 是否覆盖已存在的输出文件。

导出使用文档副本，不会修改源文件；任务在后台 STA 线程执行，逐个文件显示成功/失败/跳过状态。PDF 输出为 `名称.pdf`；PNG/TIFF 全部页输出为 `名称-p01.png`、`名称-p02.png`（TIFF 为 `.tif`），当前页输出为 `名称.png` / `名称.tif`。每次运行会在 Host 的 `data\logs` 目录写日志，最近一次输入/输出/格式/页范围/DPI 保存在 `data\settings.json`。空页在图片导出时会跳过并记录警告。

只读验证 COM 连接：

```powershell
powershell -ExecutionPolicy Bypass -File scripts\smoke-test.ps1
```

适配层集成验证：

```powershell
powershell -ExecutionPolicy Bypass -File scripts\integration-test.ps1
powershell -ExecutionPolicy Bypass -File scripts\adapter-batch-test.ps1
```

GMS → Host → COM 端到端验证：

```powershell
powershell -ExecutionPolicy Bypass -File scripts\e2e-test.ps1
powershell -ExecutionPolicy Bypass -File scripts\e2e-batch-test.ps1
```

批量导出 UI 验证（会自动创建测试 `.cdr`）：

```powershell
powershell -ExecutionPolicy Bypass -File scripts\ui-batch-test.ps1
powershell -ExecutionPolicy Bypass -File scripts\ui-batch-images-test.ps1
```

## 当前状态

- [x] 解决方案骨架与构建脚本
- [x] CorelDRAW COM 附加与文档信息读取（已在本机验证）
- [x] WPF Host、功能清单、文档信息视图
- [x] 文件夹批量导出 PDF / PNG / TIFF（全部页/当前页、DPI、后台 STA，已在本机验证）
- [x] 批量导出日志与最近设置持久化
- [x] 单元测试（32 个）
- [x] 部署、冒烟测试、适配层集成测试、UI 与 GMS 端到端测试脚本
- [x] GMS 自动构建
- [x] 部署脚本与文档
- [ ] GMS 菜单/工具栏自动注册
- [ ] 批量转换 / 重命名 / 预检等其他批量能力
- [ ] 印前 / 绘图 / 面板功能
- [ ] 全局异常处理

## 环境要求

- Windows 10/11 64 位
- CorelDRAW Technical Suite 2025（v26）
- .NET Framework 4.8.1
- 构建：Visual Studio 2022 或 .NET SDK（首次还原需要 NuGet）
