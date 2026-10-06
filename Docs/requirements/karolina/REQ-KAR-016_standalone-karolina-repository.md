# Karolina 独立源码仓库

## 目标实现

Karolina 的应用源码、构建入口、测试与运行资源独立存放于 `E:\Github\Karolina`，并作为独立 Git 仓库发布到 GitHub。Karolina 的产品需求、计划、规则与事实必须存放在当前连接工程的 `Docs` 目录，不放在应用源码仓库。帧同步工程可作为 Karolina 管理和测试的 Unity 项目；其 Gameplay、素材、任务状态和其他工程文件不得混入 Karolina 仓库。

## 技术方案

新仓库只包含 Karolina Core/Desktop/Tests、应用实际运行资源和开发构建辅助脚本；不包含产品 `Docs` 目录。产品文档由所选 Unity 工程拥有，随工程切换，不从源码仓库复制。Unity `Library`/`Temp`/`Obj`/`Builds`、Karolina 生成输出和状态、帧同步 Gameplay 代码/资源、三狼改动以及原始大型美术分层源文件均排除在 Git 版本之外。Release 输出位于仓库内 `artifacts/current`。用户日常直接运行 `Karolina.Desktop.exe`；目标工程从本机 `%LOCALAPPDATA%/Karolina/last-project.txt` 读取，桌面快捷方式直接指向仓库内该 EXE，不经过命令解释器且不要求输入启动参数。

独立仓库重新初始化 Git 历史，不复制帧同步仓库的既有提交；只提交经审查的 Karolina 文件。发布前编译独立解决方案、运行 Karolina 自带检查和项目图谱真实工程扫描；远端选择默认私有，使用当前 GitHub 登录身份创建同名仓库及推送。帧同步仓库不创建提交、不更换远端、不推送其未提交改动。

## 边界情况

目标目录非空且包含用户文件、已是 Git 仓库或远端同名项目存在时，不覆盖或替换。大型 PSD/分层图、用户运行态数据、密钥和本机路径不可因递归复制而意外进入远端。文件移动先比对源/目标清单及哈希；仅在目标副本完整后清理源中的 Karolina 专属源码和文档。若运行实例锁住源目录，则保留源文件并说明需要退出后完成清理，不强制终止。

远端权限、网络、仓库已存在或分支保护导致创建/推送失败时，保留已验证的本地仓库和提交并如实报告；不对 Unity 工程远端做任何 push。目标路径与远端无关的名称冲突不以删除解决。

## 验收条件

- `E:\Github\Karolina` 含独立解决方案、应用源码、必需运行资源及构建说明，不含产品 `Docs`、帧同步 Gameplay 文件或三狼改动。
- 双击 `artifacts/current/Karolina.Desktop.exe` 和桌面快捷方式都直接启动应用，并连接本机 `last-project.txt` 指定的 FrameSyncMoba 工程；不通过命令行启动。
- 新仓库 Git 历史只含 Karolina 职责文件；`.gitignore` 排除构建/运行产物和原始大型源素材。
- 本地 Debug/Release 构建与自带测试通过；应用对帧同步工程的图谱扫描或隔离已知问题场景产生可读、可复核结果。
- GitHub 仓库为私有并存在对应提交；帧同步仓库工作区的既有修改/未跟踪文件哈希无变化，且未提交或推送。

## 附录

迁移范围按文件用途界定。运行使用的 WebView 页面、主题、字体许可和图标必须纳入；Design 原始 PSD/大图只做本地归档，不默认公开；与三狼重做相关的材料和一次性 Unity 文档迁移工具不进入独立发布仓库。Karolina 产品文档由当前连接工程的 Docs 持有；源码仓库仅保留面向开发者的 README、AGENTS 与主题说明。

## 需求演进

### 2026-10-05 · 首次建立

用户明确要求将 Karolina 工程搬离帧同步 Moba 项目、建立独立仓库并推送 GitHub，并授权用原项目或隔离问题场景进行 Karolina 验证。

### 2026-10-05 · 文档归属校正

用户明确要求 Karolina 产品文档只保存在其当前依附的工程目录。源码、程序资源和测试留在 `E:\Github\Karolina`；需求、计划、规则、事实和目录索引归还连接的 Unity 工程 `Docs`。桌面启动快捷方式默认打开当前 FrameSyncMoba 工程，仓库通用入口仍支持记忆工程或显式指定工程。

### 2026-10-05 · 改为 EXE 直接启动

用户明确要求从 Karolina 仓库内直接运行桌面 EXE，不通过 FrameSyncMoba 命令行或命令包装器启动。Release EXE 放在 `artifacts/current`，桌面快捷方式直接指向该文件；EXE 使用本机记忆路径 `%LOCALAPPDATA%/Karolina/last-project.txt` 连接目标工程。
