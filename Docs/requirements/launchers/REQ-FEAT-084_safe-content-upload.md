# 安全分片上传与可选发布包

## 目标实现

构建正式 Player 后可单独选择生成签名 CDN 发布内容。

## 技术方案

正式客户端固定 Builds/Demo/Game/AAALOL.exe，CDN 打包默认不启用；开启后成功 Player 才生成 schema-v3 清单和内容分片。上传按哈希与清单校验、受限重试。

## 边界情况

不修改测试 Builds/UosClient；上传失败不能报告发布成功；私钥不进库；Release ZIP 仅在用户接受后进入 Git。

## 验收条件

- 正常路径达到上述目标，并由所属模块唯一拥有权威状态。
- 以上边界情况有明确返回、拒绝或可见失败；不得用占位成功代替行为。
- 确定性逻辑比较重复执行、改变插入顺序以及捕获/恢复/重演结果；只读界面不得改变 Gameplay。

## 工程实际核查

本期检索的是当前源码和测试定义。存在实现不等于完成全部需求，存在测试不等于本期已运行通过。

- `Tools/UosGameLauncher/CdnPackageBuilder.cs`：当前关联实现定义 CdnPackageBuilder（以源码为实际命名）。

### 已有测试位置

- `Assets/Scripts/Bootstrap/Tests/EditMode/UosBuildMenuTests.cs`：CombinedUosBuild_HasOneClickMenuEntry、ClearBuildGuard_ClearsCombinedUosGuard、ReleaseClientBuild_HasOptionalWindowEntry、ReleaseClientBuild_UsesIsolatedAaalolOutput、ReleaseClientBuild_ComposesValidatedCdnPackagerInvocation、ReleaseClientBuild_RefusesToCleanAnyOtherDirectory。
- `Tools/UosGameLauncher/LauncherSelfTest.cs`：含关联测试引用，具体行为需复核。

## 附录：精确接口、公式与配置

附录保留本功能必须精确表达的接口、运算和配置语义，原系统章节已拆到所属功能。若与上面的已接受修订仍有冲突，进入待确认清单而不是按旧版本执行。

本功能的明确算法、配置和边界已经写入上文；实现计划应继续补足具体数据结构、数值或资源位置。
