# 架构

## 目标

面向内部团队的 CorelDRAW 2025 效率工具。已覆盖文档信息与文件夹批量导出 PDF，后续在同一骨架上扩展批量处理、印前/制版、绘图/排版、面板/对象管理能力。

## 运行时形态

```
CorelDRAW (64-bit)
   │
   │  用户点击宏 / 工具栏按钮
   ▼
GMS 壳（VBA，极薄）── Shell 启动 ──► CorelDRAWTool.Host.exe（WPF, net48）
                                          │
                                          │ COM 附加（ROT / ProgID 激活）
                                          ▼
                                     CorelDRAW.Application
                                          │
                                          │ 通过 Corel.Interop.VGCore 调用对象模型
                                          ▼
                                     Corel 文档 / 页面 / 图层 / 图形
```

GMS 壳只做一件事：找到并启动 Host，传入命令 ID。业务逻辑、UI 和 CorelDRAW 操作都在 .NET 侧。

## 项目职责

| 项目 | 目标框架 | 职责 | 禁止 |
|---|---|---|---|
| `CorelDRAWTool.Core` | net48 | 纯业务模型、功能清单、批量导出计划器 | 引用 Corel 互操作类型 |
| `CorelDRAWTool.Corel` | net48 | 唯一 COM 适配层；附加、读取文档、批量导出 PDF | 依赖 WPF |
| `CorelDRAWTool.Host` | net48 / WPF | UI、命令编排、文档信息、批量导出 STA 执行 | 直接引用 `Corel.Interop.VGCore` |
| `CorelDRAWTool.Shell` | VBA | 菜单/按钮入口、启动 Host | 承载业务逻辑 |
| `tests/CorelDRAWTool.Core.Tests` | net48 | 核心逻辑与批量计划器单元测试 | 依赖 CorelDRAW |

## 批量导出 PDF 数据流

```
BatchExportView / BatchExportViewModel（UI 线程）
   │ 1. BatchExportPlanner.CreatePlan（纯逻辑：枚举 .cdr、计算输出、跳过已存在）
   │ 2. 启动专用 STA 工作线程
   ▼
CorelDrawSession（工作线程内独立 session）
   │ 3. ExportDocumentToPdf(source, output)
   │    OpenDocumentAsCopy(source)
   │    PublishToPDF(output)
   │    Dirty = false; Close()
   ▼
Progress<BatchExportProgress> ──► UI 更新逐文件状态 / 进度条 / 汇总
```

- 导出使用文档副本，不修改源文件。
- 取消只在文件之间生效；当前文件会完成导出。
- PDF 使用 CorelDRAW 默认 PDF 设置；后续可增加导出预设。

## 关键决策

1. **进程外 Host + COM，而不是 VBA 里 `CreateObject` 加载 .NET COM DLL。**
   - 不需要 regasm 注册、不需要管理员权限、不受 x64 位数与版本升级影响。
   - Host 可以独立调试、独立升级；崩溃不会拖垮 CorelDRAW。
   - VBA 只保留稳定的启动器，功能迭代不用反复改 GMS。

2. **net48 + Corel 官方互操作程序集，而不是 .NET 8/10。**
   - `Corel.Interop.VGCore.dll` 是 .NET Framework 4.0 运行时程序集，直接强类型引用最稳。
   - 目标机器已安装 .NET Framework 4.8.1，无需额外运行时。
   - .NET 8+ 若附加到已运行的 CDR 实例，需要自行实现 ROT 附加并失去强类型，第一版不值得。

3. **GMS 壳自动构建。**
   - `.gms` 是带 VBA 工程的二进制容器；`scripts\build-shell.ps1` 通过 `GMSManager.Projects.Create` 创建工程，用 VBE 导入 `.bas`，再执行 VBE 的 Save 命令（Id=3）保存，最后卸载工程。
   - 自动构建要求 CorelDRAW 正在运行且 VBA 项目对象模型可访问；手工导入作为兜底（见 `shell\README.md`）。
   - 模块名是 `CorelDRAWToolShell`，宏路径为 `CorelDRAWToolShell.ShowCorelDRAWTool` 和 `CorelDRAWToolShell.ShowBatchExport`。

4. **浮动窗口，而不是 CDR 内部 Docker。**
   - 第三方要做真正的停靠面板，需要 Corel 的 native addon 接口，目前没有公开 SDK。
   - 第一版使用独立 WPF 窗口；窗口可以置顶/并排使用。若后续必须内嵌，需要重新评估 native/VSTA 方案，Core 层不受影响。

## COM 附加方式

CorelDRAW 正常启动时不会把 `Application` 注册到 ROT，`Marshal.GetActiveObject` 会返回 `MK_E_UNAVAILABLE`。实测发现，用 ProgID 做 COM 激活（`Activator.CreateInstance(Type.GetTypeFromProgID("CorelDRAW.Application"))`）会转发到已运行的实例，不会新开进程。因此 `CorelDrawSession.Attach` 采用：

1. 先尝试 `GetActiveObject`（兼容以 `/Automation` 启动的场景）；
2. 失败且检测到 `CorelDRW` 进程时，再走 COM 激活。

不要把它简化成只有 `GetActiveObject`，否则在正常启动的 CorelDRAW 上会附加失败。

## 部署布局

```
%APPDATA%\Corel\CorelDRAW Technical Suite 2025\Draw\GMS\
├─ CorelDRAWTool.gms
└─ CorelDRAWTool\
   └─ app\
      ├─ CorelDRAWTool.Host.exe
      ├─ CorelDRAWTool.Core.dll
      ├─ CorelDRAWTool.Corel.dll
      ├─ Corel.Interop.VGCore.dll
      └─ ...
```

GMS 宏通过 `Application.GMSManager.Projects("CorelDRAWTool").FullFileName` 取自身路径，再定位同目录的 Host。不要使用 `%LOCALAPPDATA%\CorelDRAWTool`：Claude Code 桌面会话的 sandbox 会把该路径重定向到 `Packages\Claude_...\LocalCache`，CorelDRAW 进程不可见。

## 扩展方式

新增一个功能时：

1. 在 `FeatureCatalog` 登记 `FeatureDescriptor`，把 `isAvailable` 设为 `true`（或先登记为 `false` 占位）。
2. 纯逻辑放在 `CorelDRAWTool.Core`，补单元测试。
3. 需要 CorelDRAW 的操作在 `CorelDRAWTool.Corel` 增加方法，并保持接口 `ICorelDrawSession` 为边界。
4. 在 Host 增加对应视图/命令；`MainViewModel` 按选中的功能 ID 切换内容。
5. 如果功能需要独立入口，GMS 壳可新增一个宏并传不同的 `--command=`。

## COM / 线程约定

- 文档信息等短操作在 WPF UI 线程（STA）执行。
- 批量导出在专用 STA 工作线程执行，并在该线程内创建独立 `CorelDrawSession`；不要从 MTA 后台线程直接调用 COM。
- 通过 `Progress<T>` 把工作线程进度回报到 UI 线程，不要在工作线程直接操作 WPF 控件。
- CorelDRAW 一次只能处理一个自动化请求；避免在它正忙（例如用户拖动对象）时执行大批量操作。
- 不要在字段中长期持有过多的 COM 对象；当前只保留 `IVGApplication`，其余对象用完即释放。
- 关闭 Host 时释放 `IVGApplication` RCW；不要调用 `Application.Quit()`，避免关掉用户的 CorelDRAW。

## 已知限制

- 只验证了 CorelDRAW 2025（v26）；跨版本需要重新确认 ProgID、互操作版本和 API 差异。
- 浮窗不能像原生 Docker 一样停靠在 CorelDRAW 窗口内部。
- GMS 自动构建依赖 CorelDRAW 正在运行且 VBA 项目对象模型可访问；手工导入作为兜底。
- 批量导出 PDF 使用 CorelDRAW 默认 PDF 设置，暂未提供页面范围、压缩、字体嵌入等预设。
- 取消只在文件之间生效，当前文件会完成导出；复杂文档的 PDF 可能很大。
- 目前没有日志系统；错误显示在 Host 底部状态栏。
