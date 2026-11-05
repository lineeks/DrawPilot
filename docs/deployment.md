# 部署与调试

## 前置条件

- Windows 10/11 64 位。
- CorelDRAW Technical Suite 2025（v26）已安装。
- .NET Framework 4.8.1 已安装。
- 构建机：Visual Studio 2022 或 .NET SDK；首次还原需要访问 NuGet。

## 构建

```powershell
powershell -ExecutionPolicy Bypass -File scripts\build.ps1
```

或直接：

```powershell
dotnet build DrawPilot.sln -c Release
```

如果 CorelDRAW 安装路径不同：

```powershell
dotnet build DrawPilot.sln -c Release -p:CorelDrawDir="D:\Program Files\Corel\CorelDRAW Technical Suite\26"
```

## 测试

```powershell
dotnet test DrawPilot.sln -c Release
```

## 部署 Host

```powershell
powershell -ExecutionPolicy Bypass -File scripts\deploy.ps1
```

脚本会：

1. 构建 Release；
2. 把 `src\DrawPilot.Host\bin\Release\net48\*` 复制到 `%APPDATA%\Corel\CorelDRAW Technical Suite 2025\Draw\GMS\DrawPilot\app`；
3. 如果存在 `shell\build\DrawPilot.gms`，复制到 `%APPDATA%\Corel\CorelDRAW Technical Suite 2025\Draw\GMS`。

Host 与 GMS 放在同一目录下，GMS 宏会从自身路径推导 Host 位置。这样也避开了当前开发会话的 sandbox 对 `%LOCALAPPDATA%` 的重定向（被重定向的文件 CorelDRAW 看不到）。Host 的 `data` 子目录（`...\GMS\DrawPilot\data`）存放设置和日志，重新部署 `app` 不会删除它。

部署 Host 不需要管理员权限。更新 .NET 功能时只需重新运行部署脚本，不需要动 GMS。

## 安装/更新 GMS 壳

自动构建（推荐，CorelDRAW 需运行）：

```powershell
powershell -ExecutionPolicy Bypass -File scripts\build-shell.ps1
powershell -ExecutionPolicy Bypass -File scripts\deploy.ps1
```

或者在部署时一并构建：

```powershell
powershell -ExecutionPolicy Bypass -File scripts\deploy.ps1 -BuildShell
```

`build-shell.ps1` 生成的 `shell\build\DrawPilot.gms` 会被 `deploy.ps1` 复制到用户 GMS 目录。重启 CorelDRAW 后运行宏 `DrawPilotShell.ShowMain`（文档信息）或 `DrawPilotShell.ShowBatchExport`（批量导出）；可选在“工具 > 自定义”中分配工具栏按钮或快捷键。

如果自动构建失败（例如 VBA 项目对象模型被限制），按 `shell\README.md` 的手工步骤导入 `.bas` 并保存为 GMS。

## 运行

### 文档信息

1. 启动 CorelDRAW，打开一个文档；
2. 运行 `DrawPilotShell.ShowMain`；
3. Host 会附加到当前 CorelDRAW 实例，显示文档信息；
4. “刷新”重新读取；“插入测试矩形”会在当前文档中心插入一个测试图形（会修改文档，注意保存）。

### 批量导出

1. 启动 CorelDRAW；
2. 运行 `DrawPilotShell.ShowBatchExport`，或在 Host 左侧选择“批量导出”；
3. 选择包含 `.cdr` 的输入文件夹和输出文件夹；
4. 选择格式（PDF / PNG / TIFF）、页面范围（全部页 / 当前页）；图片还可选 150/300/600 DPI；
5. 勾选是否覆盖已存在的输出文件，点击“开始导出”。

导出使用文档副本，不修改源文件；状态栏和文件列表显示进度与结果。命名规则：

- PDF：`名称.pdf`；
- PNG/TIFF 全部页：`名称-p01.png`、`名称-p02.png`（TIFF 为 `.tif`）；
- PNG/TIFF 当前页：`名称.png` / `名称.tif`。

每次运行会在 `%APPDATA%\Corel\CorelDRAW Technical Suite 2025\Draw\GMS\DrawPilot\data\logs` 写日志；最近一次输入/输出/格式/页范围/DPI/覆盖选项保存在同目录的 `settings.json`。空页在图片导出时会跳过并记为 warning。导出期间不要操作当前文档；取消只在文件之间生效，当前文件会完成导出。

## 只读验证

```powershell
powershell -ExecutionPolicy Bypass -File scripts\smoke-test.ps1
```

该脚本只读取版本、文档、页面、图层和选中对象数量，不修改文档。

## 批量与 GMS 验证

```powershell
powershell -ExecutionPolicy Bypass -File scripts\integration-test.ps1
powershell -ExecutionPolicy Bypass -File scripts\adapter-batch-test.ps1
powershell -ExecutionPolicy Bypass -File scripts\ui-smoke-test.ps1
powershell -ExecutionPolicy Bypass -File scripts\ui-batch-test.ps1
powershell -ExecutionPolicy Bypass -File scripts\ui-batch-images-test.ps1
powershell -ExecutionPolicy Bypass -File scripts\e2e-test.ps1
powershell -ExecutionPolicy Bypass -File scripts\e2e-batch-test.ps1
```

- `adapter-batch-test.ps1` 验证 PNG/TIFF 逐页、PDF 全部/当前页、空页 warning。
- `ui-batch-test.ps1` 通过 UI Automation 驱动 PDF 批量导出并校验 PDF、日志和设置文件。
- `ui-batch-images-test.ps1` 通过设置文件加载 PNG 选项，驱动 UI 导出两页 PNG。
- `e2e-batch-test.ps1` 加载已部署的 GMS、运行 `ShowBatchExport`，确认 Host 打开到批量导出视图并包含格式选择器。

## 调试 .NET Host

- 在 Visual Studio 中打开 `DrawPilot.sln`，把 `DrawPilot.Host` 设为启动项目，F5 启动；Host 会自动附加到已运行的 CorelDRAW。
- 也可以用 `Debug > Attach to Process` 附加到 `DrawPilot.Host.exe`。
- 调试 GMS：在 CorelDRAW VBA 编辑器中单步执行；壳代码很少，通常只需确认 Host 路径与参数。

## 常见问题

| 现象 | 原因 / 处理 |
|---|---|
| `未找到正在运行的 CorelDRAW 实例` | CorelDRAW 没启动，或 Host 与 CDR 的权限级别不同（一个管理员、一个普通）。 |
| Host 启动后一直“未连接” | 先运行 `scripts\smoke-test.ps1` 验证 COM；确认 CorelDRAW 是 2025 v26。 |
| 构建时报找不到 `Corel.Interop.VGCore.dll` | 修改 `CorelDrawDir`（`-p:` 或环境变量 `CORELDRAW_DIR`）。 |
| `deploy.ps1` 提示文件被占用 | 先关闭正在运行的 Host，再部署。 |
| GMS 中找不到宏 | 检查 GMS 是否在用户 GMS 目录，重启 CorelDRAW；确认工程已保存。 |
| 插入测试矩形失败 | 当前没有打开文档、文档没有图层，或对象被锁定。 |
| 批量导出后没有输出 | 检查输出文件夹写权限、源文件是否损坏/被占用；查看 Host 状态栏和 `data\logs` 的逐文件错误。 |
| 批量列表显示“已跳过” | 输出文件已存在且未勾选“覆盖已存在的输出文件”。 |
| PNG/TIFF 某些页缺失 | 空白页无法导出，Host 会记录 warning；这是 CorelDRAW 的限制。 |
| 批量导出很慢或 PDF 很大 | PDF 使用 CorelDRAW 默认设置；复杂文档需要时间。可取消，当前文件完成后停止。 |
| 想恢复默认设置 | 删除 `%APPDATA%\Corel\CorelDRAW Technical Suite 2025\Draw\GMS\DrawPilot\data\settings.json`。 |
| `ShellExecuteW error 2` | Host 不在 GMS 同目录；重新运行 `deploy.ps1`。不要部署到被 sandbox 重定向的 `%LOCALAPPDATA%`。 |

## 卸载

- 删除 `%APPDATA%\Corel\CorelDRAW Technical Suite 2025\Draw\GMS\DrawPilot.gms`。
- 删除 `%APPDATA%\Corel\CorelDRAW Technical Suite 2025\Draw\GMS\DrawPilot` 目录（Host、依赖、设置和日志）。
- 不涉及注册表。
