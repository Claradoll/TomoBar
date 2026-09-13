# 开发说明

## 结构

`src/TomoBar` 包含完整 C# 源码和嵌入资源：

- `Core.cs`：计时状态机、任务与统计模型、JSON 原子保存。
- `Program.cs`：生命周期、单实例标记、托盘、热键、备份与导出。
- `Taskbar.cs`：无激活置顶计时条、DPI、鼠标交互和番茄绘制。
- `UI.cs` / `Theme.xaml`：WPF 主窗口、卡片、设置与主题。
- `Startup.cs`：当前用户开机自启登记。
- `SelfTests.cs` / `UiSmoke.cs`：核心回归与桌面 UI 集成检查。
- `tomato.ico` / `app.manifest`：多尺寸原创图标和 DPI／权限声明。

目标为 x64 .NET Framework 4.8，保持 C# 5 编译兼容，不依赖 NuGet 包。Windows 自带 Framework 编译器不产生外部运行库，图标和 XAML 嵌入 exe。

## 构建

根目录 `build.ps1 -Test -Package` 会编译、运行核心测试、生成便携包及 SHA-256。输出为 `dist`，测试报告为 `artifacts`；这些目录不进入 Git。

UI smoke 必须在有 Explorer 任务栏的 Windows 桌面运行，使用独立的 `--data-dir`。它会写入示例任务、修改测试偏好、控制应用自身窗口并保存预览。使用 `--data-dir` 的实例为开发隔离模式，自启操作使用独立测试登记，不设置真实自启。

`--background` 只创建主窗口句柄，在任务栏和托盘后台运行；`--diagnostics` 在数据目录输出诊断快照；`--exit` 请求同一数据目录的实例正常保存退出。

## 设计约定

保持用户数据兼容。计时依赖 `QueryUnbiasedInterruptTime`，不使用墙上时钟计算倒计时；按本地日期拆分统计。休息不计入任务时长。日期以 UTC 保存，对旧数据缺失日期标注估算恢复。

任务栏使用独立无激活工具窗，不向 Explorer 注入或建立跨进程子窗口。任务栏样式固定为红色番茄、圆角矩形、绿／橙／黑状态提示、固定数字格宽。点击处理遵守系统双击间隔，拖动不触发点击。

自启登记仅修改 HKCU `Software\Microsoft\Windows\CurrentVersion\Run` 下的 `LittleTomato` 值，命令为引用的 exe 路径加 `--background`。不会导入／导出到任务备份中。参考 [Microsoft Run / RunOnce 文档](https://learn.microsoft.com/en-us/windows/win32/setupapi/run-and-runonce-registry-keys)。

发布包只包含程序和文档，不得包含 `data`、用户绝对路径、诊断日志或测试样例。更新版本时同步程序集版本、更新记录和 Release 说明。`main` 上构建成功的新版本会自动生成 Release；已发布同名版本不会被自动覆盖。

## 便签实现

`Notes.cs` 定义结构化内容、兼容迁移和窗口管理，`NoteEditor.cs` 使用原生 WPF RichTextBox 构造编辑器，`NotesPage.cs` 提供列表／搜索／回收站。便签与任务共用原子保存与备份。不要通过任意 XAML 加载便签内容。

使用 `LittleTomato.exe --data-dir <隔离目录> --notes-smoke --background` 运行便签专项检查，生成 notes-results.txt 和界面预览。原有 `--ui-smoke` 覆盖番茄钟与中键交互。测试目录参数会隔离数据与启动登记。

便签正文版本 1 继续兼容。编号／分点与待办组合时使用正文版本 2，`Checklist` 独立于段落 `Kind`；普通待办保留旧 `check` 表示。未知正文版本拒绝编辑但保留原文。WPF 的 `TextRange.Text` 会带入自动列表标记，选区与光标偏移应只统计可编辑字符。

`NoteChrome.cs` 负责独立便签标题栏、置顶、折叠与工作区吸附。`WM_NCLBUTTONDBLCLK` 拦截标题栏双击；`WM_MOVING` 在物理坐标中按目标显示器工作区修正候选矩形。折叠高度不写入便签的展开尺寸。
