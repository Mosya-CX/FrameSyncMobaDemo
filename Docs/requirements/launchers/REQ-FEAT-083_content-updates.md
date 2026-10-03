# 增量更新与内容寻址分片

## 目标实现

已有版本复用未改变内容，更新得到与完整安装等价结果。

## 技术方案

schema-v3 以 content/<sha256> 标识不超过 95,000,000 字节分片；旧完整内容经校验可复用，新文件按清单组装。

## 边界情况

哈希不匹配不能复用；缺旧分片时走完整获取；目标路径规范化和目录穿越检查必需。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Tools/UosGameLauncher/CdnContracts.cs`：当前关联实现定义 LauncherVersion、CdnLauncherConfig、ClientReleaseManifest、CdnPackageEntry、CdnFileEntry（以源码为实际命名）。
- `Tools/UosGameLauncher/CdnPackageBuilder.cs`：当前关联实现定义 CdnPackageBuilder（以源码为实际命名）。

### 已有测试位置

- `Tools/UosGameLauncher/LauncherSelfTest.cs`：含关联测试引用，具体行为需复核。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

本功能的明确算法、配置和边界已经写入上文；实现计划应继续补足具体数据结构、数值或资源位置。
