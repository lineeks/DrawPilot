# DrawPilot VBA 壳

这里只存放 CorelDRAW 的入口宏。业务逻辑全部在 .NET Host 中，修改功能时不需要重新导入 VBA。

## 自动构建（推荐）

前提：CorelDRAW 正在运行，VBA 已初始化。

```powershell
powershell -ExecutionPolicy Bypass -File scripts\build-shell.ps1
powershell -ExecutionPolicy Bypass -File scripts\deploy.ps1
```

`build-shell.ps1` 会：

- 用 `GMSManager.Projects.Create` 创建 `DrawPilot` GMS 工程；
- 把 `shell\vba\DrawPilot.bas` 导入为模块 `DrawPilotShell`；
- 通过 VBE 的 Save 命令保存到 `shell\build\DrawPilot.gms`；
- 卸载工程。

`deploy.ps1` 会把 GMS 复制到本机每个已安装版本的用户 GMS 目录，并把 Host 复制到 GMS 同目录的 `DrawPilot\app`：

`%APPDATA%\Corel\<产品名>\Draw\GMS\DrawPilot.gms`
`%APPDATA%\Corel\<产品名>\Draw\GMS\DrawPilot\app\DrawPilot.Host.exe`

宏会从自身 GMS 路径推导 Host 位置，因此不要单独移动 Host。

重启 CorelDRAW 后，在“工具 > 宏”中运行：

- `DrawPilotShell.ShowMain`：打开 Host，默认显示文档信息；
- `DrawPilotShell.ShowBatchExport`：打开 Host，直接进入批量导出。

> 如果 CorelDRAW 中已经加载了同名的 `DrawPilot` GMS 且有未保存修改，`build-shell.ps1` 会拒绝覆盖。请先在 VBA 编辑器中保存或关闭。

## 多版本

- `build-shell.ps1` 用**当前正在运行**的 CorelDRAW 生成 GMS，并打印使用的版本。
- `deploy.ps1` 默认把同一份 GMS 和 Host 部署到本机所有已安装版本的 GMS 目录。
- 如果某个旧版本无法加载这份 GMS，先启动该版本，运行 `build-shell.ps1`，再用 `deploy.ps1 -GmsRoot <该版本 GMS 目录>` 只部署到它。
- 用 `scripts\compat-report.ps1` 查看已安装版本、残留用户目录和关键 API 探测结果。

## 手工兜底

如果自动构建不可用（例如 VBA 项目对象模型被安全设置限制）：

1. 先运行 `scripts\deploy.ps1` 部署 Host；
2. 打开 CorelDRAW，按 `Alt+F11`；
3. 新建/打开全局宏工程，工程名 `DrawPilot`；
4. 通过“File > Import File”导入 `shell\vba\DrawPilot.bas`；
5. 保存为 `DrawPilot.gms` 到用户 GMS 目录；
6. 重启 CorelDRAW，运行 `DrawPilotShell.ShowMain` 或 `DrawPilotShell.ShowBatchExport`。

> 如果手工复制代码到模块中，请去掉第一行 `Attribute VB_Name = ...`；导入 `.bas` 时保留。

## 说明

- `.bas` 是源码，`shell\build\DrawPilot.gms` 是构建产物（已被 `.gitignore` 忽略）。
- GMS 工程名是 `DrawPilot`，模块名是 `DrawPilotShell`，因此宏路径为 `DrawPilotShell.ShowMain` 和 `DrawPilotShell.ShowBatchExport`。
- 如果 CorelDRAW 版本变化，宏路径不变；只需确认用户 GMS 目录。
