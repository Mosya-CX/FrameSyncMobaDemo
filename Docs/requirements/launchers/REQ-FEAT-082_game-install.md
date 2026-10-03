# 游戏下载校验与完整安装

## 目标实现

启动器能安全安装完整游戏并启动正式客户端。

## 技术方案

清单决定版本、文件与内容分片；下载先校验签名和 SHA-256，再在独立暂存目录组装，最终安装提交只覆盖清单所属内容。

## 边界情况

断网、取消、磁盘不足、损坏分片与中断安装可见失败；不覆盖用户保存数据和无关目录。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Tools/UosGameLauncher/CdnContracts.cs`：当前关联实现定义 LauncherVersion、CdnLauncherConfig、ClientReleaseManifest、CdnPackageEntry、CdnFileEntry（以源码为实际命名）。
- `Tools/UosGameLauncher/CdnUpdater.cs`：当前关联实现定义 CdnUpdatePhase、CdnRequiredAction、CdnDownloadException、CdnDownloadIntegrityException、GameClientRunningException（以源码为实际命名）。
- `Tools/UosGameLauncher/MainForm.cs`：当前关联实现定义 LauncherPrimaryAction、MainForm、LauncherPalette、WhiteboardPanel（以源码为实际命名）。

### 已有测试位置

- `Tools/UosGameLauncher/LauncherSelfTest.cs`：含关联测试引用，具体行为需复核。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

本功能的明确算法、配置和边界已经写入上文；实现计划应继续补足具体数据结构、数值或资源位置。
