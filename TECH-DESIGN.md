# Life with Wife and Child — 技术方案

> 状态：**v1.1 设计变更中**（v1.0 已实现并跑通；v1.1 按 DESIGN.md v0.6 重做家庭线与校历）
> 目标：Stardew Valley 1.6.15.24356 / SMAPI 4.2.1.0 / .NET 6
> 配套文档：[DESIGN.md](DESIGN.md)（产品设计，本文件只回答"怎么实现"）
>
> ⚠️ **v1.1 起，以下章节已被 §16 取代**（保留原文只作历史记录）：
>
> | 旧章节 | 状态 | 原因 |
> |---|---|---|
> | §6.2 配置项里的 `EnableSpouseCollection` / `EnableSpouseFarmWalk` | 删除 | 伴侣周一代收整块取消 |
> | §7.2 的"日程与对话注入（改写 Penny 日程）" | 删除 | 改为**只读探测**她的课时（§16.1） |
> | §7.4 伴侣周一 Keg 代收 | 删除 | 改为孩子周末收桶（§16.4） |
> | §7.5 家庭箱 | 部分保留 | 容器实现不变，**获取方式**改变（§16.4） |
> | §8 补丁清单里的 `Object.placementAction` | 保留 | 家庭箱放置仍需要 |
> | §9 资产键表里的 `Characters/schedules/Abigail` | 删除 | 不再改任何 NPC 日程 |
> | §11 里程碑 / §12 指令与回归清单 | 增补 | 见 §16.7 / §16.8 |
>
> 其余章节（构建、工程结构、成长接管、出门方案 B、存档策略、补丁清单）继续有效。
>
> ⚠️ 实现期已作废的两个前提：
> 1. **§3.1 的"net6.0 编不过"已不成立**——本机 NuGet 现在可达，`dotnet restore` 能直接取到 6.0 引用包，`net6.0` 一路编译。方案 A（补 ref pack）不再是 P0 前置。
> 2. **§8 的 `Child.MovePosition` 补丁不需要**——反编译确认它是一次完整的四向移动重写，没有年龄位置钳制（详见 milestones.md §2）。
>
> 本方案中的「已验证」= 我在本机对游戏与 SMAPI 程序集做过反编译/构建实测，
> 不是推测；「待验证」= 必须在对应里程碑做 spike 后才能定案。

---

## 0. 结论摘要

**技术栈一句话**：C# 10 + `net6.0` 类库 + SMAPI 4.2.1 原生 Mod + Harmony 2.2.2 补丁 + SMAPI 资产编辑 API（`AssetRequested`）+ `modData` 持久化 + JSON i18n，**零第三方 NuGet 依赖、零框架 Mod 依赖**。

| 层 | 选型 | 版本锁定 |
|---|---|---|
| 语言 / 运行时 | C# 10，`<TargetFramework>net6.0</TargetFramework>` | 跟随 SMAPI 4.2.1 的 net6 宿主，不可改 net8 |
| Mod 框架 | SMAPI `Mod` 子类 + `IModHelper` | 4.2.1，`MinimumApiVersion 4.2.0` |
| 补丁 | `HarmonyLib`（引用游戏目录 `smapi-internal\0Harmony.dll`，不复制、不打包） | Harmony **2.2.2.0** |
| 内容数据 | `helper.Events.Content.AssetRequested` + `asset.Edit<T>()` | SMAPI 4.x API |
| 持久化 | 仅 `modData`，不新增序列化类型 | — |
| 本地化 | `i18n/default.json` / `zh-CN.json` + `helper.Translation` | — |
| 配置面板 | `IGenericModConfigMenuApi` 软依赖（可选安装） | 无硬依赖 |
| 构建 | 手写 `.csproj` + `HintPath` + `AfterBuild` 复制 | 离线，不用 ModBuildConfig |
| 调试 | `helper.ConsoleCommands` 自定义指令 + `ILSpy` 读原版 | — |

**三条硬约束**（来自实测，违反必踩坑）：

1. Mod 必须 `net6.0`：SMAPI 4.2.1 的 `runtimeconfig.json` 是 `tfm: net6.0`，net8 产物无法加载。
2. 不能用 NuGet：外网（含国内镜像）全部不可达，`Pathoschild.Stardew.ModBuildConfig` 装不上，也没有 .NET 6 targeting pack。
3. 不打包 `0Harmony.dll`：直接引用 `smapi-internal\0Harmony.dll` 并设 `Private=false`，运行时由 SMAPI 提供。

---

## 1. 环境事实与设计稿校正

### 1.1 实测环境快照

| 项 | 实测值 | 备注 |
|---|---|---|
| `Stardew Valley.dll` | 1.6.15.24356，6,268,416 bytes | 与 DESIGN.md 一致 |
| `StardewModdingAPI.dll` | 4.2.1.0 | 与 DESIGN.md 一致 |
| SMAPI 运行时 | `runtimeconfig.json` → `tfm: net6.0`，`Microsoft.NETCore.App 6.0.0`（rollForward latestMinor） | **Mod 必须 net6 兼容** |
| 游戏运行时 | `Stardew Valley.runtimeconfig.json` → `Microsoft.NETCore.App 6.0.32` | 自包含部署 |
| `smapi-internal\0Harmony.dll` | **241,152 bytes，版本 2.2.2.0** | DESIGN.md §5.1 写的"24 KB 级、Harmony 4.x"**有误** |
| 本机 .NET SDK | 仅 8.0.419；`packs` 里只有 8.0.25 的 ref pack | **没有 6.0 targeting pack** |
| NuGet 连通性 | `api.nuget.org` 与腾讯/华为/Azure 中国镜像均连接失败 | 确认离线 |
| 已装 Mod | ContentPatcher、SaveBackup | SaveBackup 可用于回归测试 |
| 游戏目录 DLL | `MonoGame.Framework.dll`、`xTile.dll`、`Stardew Valley.xml`、`smapi-internal\Newtonsoft.Json.dll`（13.0.3） | 引用所需均在 |

### 1.2 原版 aging 的真实阈值（已从 IL 确认）

`Child.dayUpdate(int dayOfMonth)` 的逻辑：`daysOld += 1`，然后按 `daysOld` 设置 `NPC.Age`：

| 条件 | 结果 |
|---|---|
| `daysOld >= 55` | `Age = 3`，`speed = 4` |
| `daysOld >= 27` | `Age = 2` |
| `daysOld >= 13` | `Age = 1` |
| 否则 | `Age = 0`（婴儿，固定在婴儿床附近） |

> 设计稿说"阈值在 `Child` 代码里，实现时读代码确认"——**现已确认，是 13 / 27 / 55，不是 14 / 28 / 56**。
> 这条事实决定了两件事：① 7 天/级必须完全接管，不能复用原版节奏；② 存档迁移时可以用 `daysOld` 精确反推原版阶段（见 §6.4）。

### 1.3 其它已确认的 API 事实

| 事实 | 用途 |
|---|---|
| `Child : NPC`，自有字段仅 `daysOld`(NetInt)、`newborn/baby/crawler/toddler`(常量 0–3)、`previousState`、`hat`、`mutex`、`setStateEvent` | 年龄不存副本，统一走父类 `NPC.Age` |
| `Child` 只重写了 `update`、`draw`、`MovePosition`、`dayUpdate`、`tenMinuteUpdate`、`updateSlaveAnimation`、`IsVillager` | 补丁面很窄，见 §8 |
| `Child.update(GameTime, GameLocation)` 仅 49 条 IL：轮询事件、更新 mutex、**直接调用 `NPC.update`**，再处理跳跃/位移 | "行为接管"风险远低于设计稿的预估，见 §7.2 |
| 屋内乱走 / 婴儿床行为在 `Child.tenMinuteUpdate()`（Age 2/3 分支调用 `FarmHouse.GetRandomOpenPointInHouse`、`GetChildBedSpot`） | 离家时只需 patch 这一个方法 |
| `Child.MovePosition(...)` 466 条 IL，先调 `NPC.MovePosition` 再做年龄相关的位置钳制 | 离家时是否可放行 = P2 spike 的核心问题 |
| 孩子实体存放在 `FarmHouse.characters`（`FarmHouse.getChildren()` = `characters.OfType<Child>()`） | 场景切换只需增删 `location.characters` |
| `Farmer.Children` 属性（`getChildren() -> List<Child>`） | 遍历孩子用 `Game1.player.Children` |
| `GameLocation.addCharacter(NPC)`、`characters` 为 `NetCollection<NPC>`、`Game1.getLocationFromName(string)` | 出门/回家搬迁 |
| `NPC` 有 `Schedule`、`ScheduleKey`、`TryLoadSchedule(...)` 4 个重载、`checkSchedule(int)`、`parseMasterSchedule(string,string)`、`pathfindToNextScheduleLocation(...)` | 走日程 |
| 日程资产键实测为 **`Characters\schedules\<NPC名>`**（`getMasterScheduleRawData` 里 `ldstr "Characters\schedules\"`） | 与 DESIGN.md §5.3 一致 |
| `Chest : Object`，有 `playerChest`、`Items`、`GetItemsForPlayer()`、`GlobalInventoryId` | 家庭箱实现 |
| `Object.readyForHarvest` / `heldObject` / `MinutesUntilReady` | Keg 代收判定 |
| SMAPI 4 资产事件类型 `AssetRequestedEventArgs`（`Name`/`DataType`/`Edit<T>()`） | 内容注入 |

---

## 2. 技术选型与理由

### 2.1 语言与运行时

- **C# 10 / net6.0**。理由：SMAPI 4.2.1 自身跑在 .NET 6 上，mod 程序集必须能被 .NET 6 加载；C# 10 是 .NET 6 的默认语言版本，`record`、`file` 作用域、模式匹配都够用。
- 不用 `netstandard2.0`：虽然能编，但会丢掉 `Spans`/`System.Text.Json` 等 API，且 SMAPI 对非 net6 目标可能出现兼容性提示。
- 不用 `net8.0`：**能编译但加载不了**，属于隐性坑。
- 不开 `PublishSingleFile` / `ReadyToRun`：SMAPI 需要可反射的普通程序集。

### 2.2 Mod 框架：SMAPI 原生，不用框架 Mod

- 只依赖 SMAPI 本身，不引入 SpaceCore / StardewUI / Content Patcher 作为硬依赖。
- 理由（与 DESIGN.md §5.4 一致）：用户少装前置、冲突面小、卸载干净。
- Content Patcher 作为**可选**：游戏目录已装 CP，但本 mod 不读 CP 的 API，只保证两者共存（都用 `AssetRequested` 时按 SMAPI 的编辑链正常叠加）。

### 2.3 补丁层：Harmony 2.2.2

- 引用 `$(GamePath)\smapi-internal\0Harmony.dll`，`<Private>false</Private>`，**不复制到输出目录**。
- 统一用 `[HarmonyPatch]` 特性 + `harmony.PatchAll()`，补丁类集中在 `src/Patches/`，每个补丁带注释说明"为什么补这里"。
- 所有补丁必须 `try/catch` 包裹并降级：补丁抛异常时记录日志、关闭对应子系统，不能让游戏崩。

### 2.4 内容层：SMAPI 资产编辑

用 `helper.Events.Content.AssetRequested`：

```csharp
helper.Events.Content.AssetRequested += (_, e) =>
{
    if (e.Name.IsEquivalentTo($"Characters/schedules/{name}"))
        e.Edit(asset => asset.AsDictionary<string, string>().Data[...] = "...", AssetEditPriority.Default);
};
```

约定：
- 一切对原版数据的修改都是**增量**（追加键、插入条目），绝不整表替换。
- 孩子相关的键在名字确定后（`DayStarted` / 命名事件后）动态注册，并调用 `helper.GameContent.InvalidateCache(key)` 重建。
- 编辑顺序用 `AssetEditPriority` 明确表达依赖（CP 默认 `Default`，我们尽量用 `Default` 或 `Late` 以免被覆盖）。

### 2.5 持久化：只用 `modData`

- 全部状态写入 `child.modData`，键名统一 `lwwc/` 前缀。
- **不新增 `[Serializable]` 类型**，不写自定义 `SaveData`：卸 mod 后存档仍可读，孩子退回原版行为。
- 所有读取都走一个 `ChildState` 包装类，带默认值兜底，避免旧存档缺键导致 NRE。

### 2.6 明确不采用的技术

| 方案 | 不采用的原因 |
|---|---|
| `Pathoschild.Stardew.ModBuildConfig` | NuGet 不可达；手写 csproj 已能覆盖其全部功能 |
| 打包自有 `0Harmony.dll` | 与 SMAPI 自带的版本冲突，SMAPI 会告警甚至崩 |
| 自定义 `SaveData` / `ISerializable` | 违反"卸 mod 不坏档"目标 |
| 代理 NPC（方案 A） | 双实体存档同步 bug 更难查；仅作降级预案 |
| 新建地图 / 新 NPC | 与 §5.4 的内容策略冲突，且美术成本不可控 |
| 单元测试框架（xunit/NUnit） | 离线拿不到包；对 Unity 主循环代码收益极低，改用调试指令 + 回归清单 |
| 多人联机支持 | v1 明确不支持（DESIGN.md §7），代码里只做"检测到多人则禁用成长逻辑并提示" |

---

## 3. 构建与部署（离线方案）

### 3.1 现状：`net6.0` 在本机编不过

实测（清空 NuGet 源后 `dotnet build`）：

| TFM | 结果 |
|---|---|
| `net6.0` | ❌ `NU1100: 无法解析 Microsoft.WindowsDesktop.App.Ref (= 6.0.36)` 等 |
| `net8.0` | ✅ 能编，但 SMAPI 加载不了，无意义 |
| `netstandard2.1` | ✅ 能编（引用游戏 DLL，43 warning / 0 error） |

原因：本机只有 .NET 8 SDK，没有 6.0 targeting pack，而 restore 需要从 NuGet 下载 `Microsoft.NETCore.App.Ref 6.0.36` / `Microsoft.WindowsDesktop.App.Ref 6.0.36` / `Microsoft.AspNetCore.App.Ref 6.0.36`。

### 3.2 方案 A（推荐）：补齐 6.0 引用包，保持 `net6.0`

任选其一：

1. **装 .NET 6 SDK**（离线安装包），之后 `packs\Microsoft.NETCore.App.Ref\6.0.x` 自动就位。
2. **手工放 ref pack**：在任意可联网机器上从 nuget.org 下载三个 `.nupkg`：
   - `Microsoft.NETCore.App.Ref` 6.0.36
   - `Microsoft.WindowsDesktop.App.Ref` 6.0.36
   - `Microsoft.AspNetCore.App.Ref` 6.0.36

   放到仓库外的本地目录（如 `%USERPROFILE%\.nuget\local-packs\`），在仓库根加一个 `nuget.config`：

   ```xml
   <?xml version="1.0" encoding="utf-8"?>
   <configuration>
     <packageSources>
       <clear />
       <add key="local" value="%USERPROFILE%\.nuget\local-packs" />
     </packageSources>
   </configuration>
   ```

   首次 restore 会把包解到全局缓存，之后完全离线可编。

> ⚠️ 这一步是 **P0 的前置条件**，必须在写第一行业务代码前解决。

### 3.3 方案 B（兜底）：`netstandard2.1` 过渡

仅用于"没有 6.0 ref pack 但想立刻开工"的场景：把 csproj 的 `TargetFramework` 改成 `netstandard2.1`，实测可编译。

**风险**：产物的 `TargetFrameworkAttribute` 不是 net6，SMAPI 是否告警/拒绝加载**未验证**。P0 阶段用 5 分钟验证一次；若 SMAPI 正常加载且指令可用，可作为过渡，但**发布版必须回到 net6.0**。

### 3.4 `.csproj`（完整）

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <LangVersion>10</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
    <AssemblyName>LifeWithWifeAndChild</AssemblyName>
    <RootNamespace>LifeWithWifeAndChild</RootNamespace>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>

    <!-- 游戏根目录：仓库位于 Mods\LifeWithWifeAndChild\，上一级是 Mods，再上一级是游戏根 -->
    <GamePath>$(MSBuildThisFileDirectory)..\..\</GamePath>
    <!-- P0 决策点：顶层 dev 目录最稳（见 §3.6） -->
    <DeployPath>$(GamePath)Mods\_LWWC_dev</DeployPath>
  </PropertyGroup>

  <ItemGroup>
    <Compile Include="src\**\*.cs" />
    <None Include="manifest.json" />
    <None Include="assets\**\*" />
    <None Include="i18n\**\*" />
  </ItemGroup>

  <ItemGroup>
    <Reference Include="Stardew Valley">
      <HintPath>$(GamePath)Stardew Valley.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="StardewModdingAPI">
      <HintPath>$(GamePath)StardewModdingAPI.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="MonoGame.Framework">
      <HintPath>$(GamePath)MonoGame.Framework.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="xTile">
      <HintPath>$(GamePath)xTile.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="0Harmony">
      <HintPath>$(GamePath)smapi-internal\0Harmony.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Newtonsoft.Json">
      <HintPath>$(GamePath)smapi-internal\Newtonsoft.Json.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>

  <Target Name="DeployMod" AfterTargets="Build">
    <ItemGroup>
      <ModFiles Include="$(TargetPath)" />
      <ModFiles Include="manifest.json" />
    </ItemGroup>
    <Copy SourceFiles="@(ModFiles)" DestinationFolder="$(DeployPath)" />
    <Copy SourceFiles="@(None)" DestinationFiles="@(None->'$(DeployPath)\%(RecursiveDir)%(Filename)%(Extension)')"
          Condition="'%(None.Identity)' != 'manifest.json'" SkipUnchangedFiles="true" />
  </Target>
</Project>
```

要点：
- `<Private>false</Private>` 全部必填：否则 6 个 DLL 会被复制进 mod 目录，SMAPI 会报重复程序集。
- `AppendTargetFrameworkToOutputPath=false`：输出到 `bin\Debug\`，部署路径简单。
- `DeployMod` 只复制 dll + manifest + assets/i18n，**不复制**引用 DLL。

### 3.5 `manifest.json`

```json
{
  "Name": "Life with Wife and Child",
  "Author": "TODO",
  "Version": "1.0.0-alpha.1",
  "Description": "孩子真正长大，阿比盖尔对每个成长阶段做出反应。",
  "UniqueID": "TODO.LifeWithWifeAndChild",
  "EntryDll": "LifeWithWifeAndChild.dll",
  "MinimumApiVersion": "4.2.0",
  "UpdateKeys": []
}
```

`UniqueID` 一旦发布不可改，P0 时定死（建议 `YourName.LifeWithWifeAndChild`）。

### 3.6 部署目录（对应 DESIGN.md §9 开放问题 1）

| 方案 | 可行性 | 结论 |
|---|---|---|
| `Mods\LifeWithWifeAndChild\deploy\` | SMAPI 可识别嵌套，但有告警，且与仓库同目录容易误加载 | 不推荐 |
| `Mods\_LWWC_dev\` | 顶层，SMAPI 无告警，路径稳定 | **推荐**，`DeployPath` 已按此配置 |

P0 用 `_LWWC_dev` 验证一次即可定案。注意：`Mods\LifeWithWifeAndChild\` 本身**不能**含 `manifest.json`，否则会被 SMAPI 当成 mod 加载一份旧的。

### 3.7 开发循环

```
改代码 → dotnet build → 启动游戏 → SMAPI 控制台看 [LWWC] 日志
```

- 日志前缀统一 `[LWWC]`，方便在 SMAPI 控制台过滤。
- 用 `helper.Monitor.Log(msg, LogLevel.Trace)` 记流程，`LogLevel.Alert` 只留给真正需要玩家注意的事。
- 调试时可用 `SMAPI --mods-path` 隔离加载；崩溃看 `%APPDATA%\StardewValley\ErrorLogs\`。

---

## 4. 工程结构

```
LifeWithWifeAndChild/
├─ DESIGN.md                  ← 产品设计（已有）
├─ TECH-DESIGN.md             ← 本文件
├─ README.md
├─ LifeWithWifeAndChild.csproj
├─ manifest.json
├─ nuget.config               ← 仅方案 A 需要
├─ src/
│  ├─ ModEntry.cs             ← 入口：Config、事件注册、指令注册、补丁初始化
│  ├─ Constants.cs            ← modData 键名、资产键模板、阶段枚举
│  ├─ ModConfig.cs            ← 配置项 + GMCM 注册
│  ├─ Core/
│  │  ├─ ChildState.cs        ← modData 读写包装（唯一状态入口）
│  │  ├─ ChildRegistry.cs     ← 找孩子、判断是否在家/在校/已成年
│  │  ├─ StageTable.cs        ← 阶段 → 能力/事件/日程的配置表
│  │  └─ Log.cs               ← 统一日志封装
│  ├─ Growth/
│  │  ├─ GrowthService.cs     ← 日结算推进、阶段变更事件、顺延队列
│  │  ├─ AgingOverride.cs     ← 接管原版 daysOld→Age 的换算
│  │  └─ SaveMigration.cs     ← 老存档折算、缺键修复
│  ├─ Away/
│  │  ├─ CommuteService.cs    ← 出门/回家、场景归属切换、读档去重
│  │  ├─ ScheduleBuilder.cs   ← 运行时生成日程/对话资产键与内容
│  │  └─ NameKey.cs           ← 名字规范化、重名检测、键映射
│  ├─ Abilities/
│  │  ├─ IChildAbility.cs     ← 能力接口（CanRun/Run/Describe）
│  │  ├─ WaterCropsAbility.cs
│  │  ├─ CollectEggsAbility.cs
│  │  ├─ HarvestCropsAbility.cs
│  │  └─ SellProduceAbility.cs
│  ├─ Family/
│  │  ├─ FamilyChest.cs       ← 家庭箱放置/识别/容量
│  │  └─ KegCollector.cs      ← Keg 扫描与产出搬运
│  ├─ Spouse/
│  │  ├─ SpouseSchedule.cs    ← 阿比盖尔周一增量日程
│  │  └─ SpouseCollection.cs  ← 到点结算、满箱提示
│  ├─ Patches/
│  │  ├─ ChildAgePatch.cs
│  │  ├─ ChildUpdatePatch.cs
│  │  ├─ ChildTenMinutePatch.cs
│  │  └─ ChildMovePositionPatch.cs
│  ├─ Content/
│  │  ├─ AssetInjector.cs     ← AssetRequested 总入口
│  │  ├─ DialogueContent.cs
│  │  ├─ EventContent.cs
│  │  └─ ItemDefinition.cs    ← 家庭箱物品
│  ├─ Commands/
│  │  └─ DebugCommands.cs     ← 全部控制台指令
│  └─ Integrations/
│     └─ GenericModConfigMenu.cs
├─ assets/
│  ├─ data/                   ← 事件脚本、日程片段、对话模板（JSON）
│  └─ i18n/{default,zh-CN}.json
└─ docs/
   ├─ milestones.md
   ├─ balance.md              ← §10 待定的数值表
   └─ test-plan.md            ← 手工回归清单
```

**依赖方向**（禁止反向引用，避免补丁层与业务层耦合）：

```
ModEntry → Growth / Away / Abilities / Family / Spouse / Content / Commands
Patches  → Core（ChildState） + Growth/Away（只读状态，不做业务决策）
Core     → 无（只依赖 SMAPI 与游戏类型）
```

补丁层原则：**patch 只做"拦截与转发"，决策逻辑放在 service 里**。这样方案 A/B 切换、功能开关都只改 service。

---

## 5. 运行时架构

### 5.1 对象协作

```
ModEntry
 ├─ ModConfig（读写 config.json）
 ├─ Harmony.PatchAll()                   → Patches/*
 ├─ Events.GameLoop.DayStarted           → GrowthService.OnDayStarted
 │                                          ├─ SaveMigration.EnsureState
 │                                          ├─ ChildState.Refresh
 │                                          └─ CommuteService.DecideToday
 ├─ Events.GameLoop.DayEnding            → GrowthService.OnDayEnding（推进阶段）
 ├─ Events.GameLoop.SaveLoaded           → CommuteService.RepairOnLoad（去重/兜底回家）
 ├─ Events.Content.AssetRequested        → AssetInjector.OnAssetRequested
 ├─ Events.Player.Warped                 → CommuteService.OnWarped（跨图兜底）
 └─ Events.GameLoop.TenMinuteUpdate?     → 不需要，走 Child 补丁
```

### 5.2 一天的事件顺序（关键）

| 时机 | 动作 | 失败兜底 |
|---|---|---|
| `SaveLoaded` | 校验孩子状态；若 `away` 但实际不在学校 → 强制回家 | 记录警告 + 回家 |
| `DayStarted` | 读阶段、决定今天是否上学/上工、注入当日日程 | 注入失败 → 孩子留家 |
| 8:00（`TenMinute` 由 `Child` 侧驱动） | 搬迁到学校/镇上 | 搬迁失败 → 留家并提示 |
| 白天 | 能力执行一次（可配置触发时刻） | 失败记日志，不重试 |
| 17:00 | 回家 | — |
| `DayEnding` | `daysInStage++`，够 7 则进阶；写 `mailReceived` flag | 写前校验，异常则不进阶 |

> 为什么推进放在 `DayEnding` 而不是 `DayStarted`：设计稿要求"睡觉结算"，且 `DayEnding` 在存档前触发，能保证"玩家看到的新一天 = 新阶段"，同时避免读档回退时重复进阶。`DayEnding` 里用一个 `lwwc/lastGrowthDay`（`Game1.Date.TotalDays`）做幂等。

### 5.3 状态唯一性

- 孩子的"当前阶段"唯一来源：`child.modData["lwwc/stage"]`。
- 孩子的"是否在外"唯一来源：`child.modData["lwwc/away"]`。
- 内存中的 `Age` 只是**派生值**（`Age = min(stage, 3)`），不允许反推阶段。
- 任何地方要判断阶段，只能走 `ChildState.Get(child)`，禁止直接读 `daysOld`。

---

## 6. 数据模型

### 6.1 `modData` 键表

| 键 | 类型 | 语义 | 写入时机 |
|---|---|---|---|
| `lwwc/stage` | int 0–6 | 当前阶段 | 进阶时 |
| `lwwc/daysInStage` | int | 当前阶段已过天数 | 每天 `DayEnding` |
| `lwwc/lastGrowthDay` | int | 上次结算的 `Date.TotalDays`，幂等用 | `DayEnding` |
| `lwwc/flags` | string | 逗号分隔的剧情 flag，如 `first_walk,first_school` | 事件触发时 |
| `lwwc/pendingEvents` | string | 未触发顺延的事件 id（逗号分隔） | 阶段变更/事件失败 |
| `lwwc/away` | bool | 当前是否在外（学校/镇上） | 出门/回家 |
| `lwwc/awayUntil` | int | 计划回家时刻（游戏内分钟） | 出门时 |
| `lwwc/homeLocation` | string | 回家落点（`FarmHouse`） | 出门时 |
| `lwwc/workDoneToday` | bool | 今日能力是否已执行 | 每天 |
| `lwwc/nameKey` | string | 规范化后的资产键名 | 命名后 |
| `lwwc/schoolLocation` | string | 当前课堂所在图 | 社区中心修复后切换 |

家庭箱不是 `Character`，用箱子自己的 `modData`：

| 键 | 语义 |
|---|---|
| `lwwc/familyChest` | `"true"` 标记这是家庭箱 |
| `lwwc/familyChestOwner` | 所属玩家 `UniqueMultiplayerID`（为未来多人留位） |

### 6.2 配置项（`ModConfig`）

```csharp
public sealed class ModConfig
{
    public int GrowthSpeed { get; set; } = 7;          // 天/级，最小 1
    public bool EnableChildWork { get; set; } = true;  // 孩子能力总开关
    public bool EnableSpouseCollection { get; set; } = true; // 周一代收总开关
    public int ChildWorkHour { get; set; } = 12;       // 能力执行时刻（游戏内小时）
    public bool EnableDebugCommands { get; set; } = true;
    public bool VerboseLogging { get; set; } = false;
}
```

- 读取：`helper.ReadConfig<ModConfig>()`，构造时 `Sanitize()` 兜底（`GrowthSpeed` clamp 到 1–60）。
- GMCM：检测到 `GenericModConfigMenu` 才注册，注册失败只记日志不影响功能。

### 6.3 成长状态机

```
        +7d            +7d            +7d            +7d            +7d            +7d
  0 婴儿 ──→ 1 学步 ──→ 2 幼儿 ──→ 3 儿童 ──→ 4 学龄 ──→ 5 少年 ──→ 6 成年
              │            │            │            │            │            │
          第一次走路    送小礼物     浇水3-5格    第一天上学     梦想/叛逆     成年礼
```

- 每级必须"新对话 + 一件能做的事 + 一个事件"（DESIGN.md §3），技术上是三张表：`StageTable.DialogueKey`、`StageTable.AbilityId`、`StageTable.EventId`。
- 事件未触发则进 `pendingEvents`，下一次阶段变更前仍可触发一次（对应 DESIGN.md §3.1 的风险对策）。
- 最大阶段 6，之后 `daysInStage` 停止累加（避免溢出和无意义写盘）。

### 6.4 存档迁移（老存档兼容）

```
读档时若 child.modData 无 lwwc/stage：
  daysOld = child.daysOld.Value
  vanillaStage = daysOld >= 55 ? 3 : daysOld >= 27 ? 2 : daysOld >= 13 ? 1 : 0
  modStage = vanillaStage
  daysInStage = 0
  写回 modData，并给玩家一条一次性提示（mail flag）
```

- 若老存档孩子已成年（原版不存在，防御性处理）：`stage = 3`。
- 若 `daysOld` 异常（负数/极大）：`stage = 0`，记警告。
- 迁移只跑一次，用 `lwwc/migrated` 标记。

---

## 7. 关键系统详细设计

### 7.1 成长接管（对应 DESIGN.md §3.1）

**问题的本质**：原版年龄由 `Child.dayUpdate` 内部写死 13/27/55 天，且 `NPC.Age` 同时驱动**行为**（婴儿躺床、学步爬、幼儿屋内走）和**绘制**（儿童精灵）。

**推荐方案：合成 `daysOld` + 派生 `Age` 的双写**

```
Patch Child.dayUpdate(int dayOfMonth) —— Prefix
  1. 读 stage = ChildState.Get(this).Stage
  2. 若 stage >= 4：把 this.daysOld 设为 55 的等价（保持 Age=3、儿童外观）
     否则：把 this.daysOld 设为该阶段的原版等价天数：
        stage 0 → 0
        stage 1 → 13
        stage 2 → 27
        stage 3 → 55
  3. 交回原版逻辑（不 skip），让 resetForNewDay / 位置重排 / speed 全部照原版跑
```

为什么这样做：

- 原版 `dayUpdate` 里的 `resetForNewDay`、`UpdateInvisibilityOnNewDay`、`mutex.ReleaseLock()`、位置重排（Age 1/2 分支）**都依赖内部状态**，整体 skip 需要逐条复刻，风险高。
- 把 `daysOld` 当成"原版年龄的投影"，原版的副作用就全部复用，我们只控制输入。
- `Age = min(stage,3)` 自然成立：stage 4/5/6 都投影到 55 → Age 3 → 儿童外观（正好是 DESIGN.md §3 的显式取舍），**不需要 patch `draw`，也不需要 patch `NPC.Age` getter**（后者会影响全村民）。

边界校验（为什么这个投影是安全的）：原版阈值是 `>=13 / >=27 / >=55`，而 prefix 写入后原版还会 `+1`。设 `synthetic` 为写入值，则实际比较的是 `synthetic+1`：

| 阶段 | 写入 | 次日实际 | 落入的 Age 区间 |
|---|---|---|---|
| 0 | 0 | 1–7（7 天内） | `<13` → Age 0 ✅ |
| 1 | 13 | 14–20 | `13..26` → Age 1 ✅ |
| 2 | 27 | 28–34 | `27..54` → Age 2 ✅ |
| 3 | 55 | 56–62 | `>=55` → Age 3 ✅ |
| 4–6 | 55 | 56–62 | `>=55` → Age 3 ✅（沿用儿童外观） |

每个阶段 7 天内都不会跨到下一档，所以阶段内 `Age` 稳定；跨阶段时恰好由 prefix 重新投影，触发原版自己的位置重排逻辑。

代价与注意：

- `daysOld` 不再单调递增（会来回跳），若其它 mod 读 `daysOld` 判断年龄会看到跳变。可接受，但要在冲突检测里提示。
- `daysOld` 是 `NetInt`，写入要保证在 master game 且非事件中。
- **P1 第一件事是确认 `Child.daysOld` 的可见性**（public 可直接写，否则用 `AccessTools` 反射或 `NetInt.Value`）。若都不可行，退化为方案 A2：
  - `Patch Child.dayUpdate` postfix：原版跑完后强制 `this.Age = min(stage,3)` + `speed` 校准。
  - 代价是位置重排会在原版阈值日（13/27/55）才发生，可能阶段性错位 1–2 格，需实测。

**推进逻辑**（`GrowthService`）：

```csharp
// DayEnding
var state = ChildState.Get(child);
int today = Game1.Date.TotalDays;
if (state.LastGrowthDay == today) return;         // 幂等

state.LastGrowthDay = today;
if (state.Stage >= 6) return;

state.DaysInStage++;
if (state.DaysInStage >= config.GrowthSpeed)
{
    state.DaysInStage = 0;
    var next = state.Stage + 1;
    // 写前校验：孩子存在、农场房屋有主、不在 cutscene
    state.Stage = next;
    QueueStageEvent(next);                        // 次日触发，失败进 pending
}
```

**调试指令**必须能绕过一切：

```
lwwc_stage <0-6>    强制设置阶段（不推进天数，直接改 modData 并刷新 Age）
lwwc_days <n>       设置 daysInStage
lwwc_info           打印全部孩子状态（阶段/天数/Age/是否在外/位置）
```

### 7.2 上学 / 出门（方案 B，对应 DESIGN.md §5.3）

#### 场景归属切换（已验证可行）

```csharp
// 出门
var farmHouse = Game1.getLocationFromName("FarmHouse") as FarmHouse;
farmHouse.characters.Remove(child);
var school = Game1.getLocationFromName(targetLocationName);
school.addCharacter(child);
child.currentLocation = school;
child.Position = new Vector2(spawnX, spawnY) * 64f;
child.modData["lwwc/away"] = "true";
child.TryLoadSchedule(nameKey);   // 加载 Characters/schedules/<nameKey>

// 回家（反向，且必须先移除再添加，避免 NetCollection 重复）
```

实测依据：孩子就在 `FarmHouse.characters` 里（`FarmHouse.getChildren()` 即对 `characters` 做 `OfType<Child>()`），所以增删集合就是"换图"。

#### 行为接管（风险比设计稿预估低）

`Child.update` 只有 49 条 IL，核心是 `NPC.update` + 跳跃/位移，真正"儿童专属"的是 `Child.tenMinuteUpdate`（Age 2/3 时在屋里乱走、回婴儿床）和 `Child.MovePosition`（年龄相关的位置钳制）。

因此补丁策略是**两个小补丁**，而不是接管整个 `update`：

| 补丁 | 行为 |
|---|---|
| `Child.tenMinuteUpdate()` Prefix | `away == true` 时直接 `return false`，禁止屋内乱走/回床 |
| `Child.MovePosition(...)` Prefix（待 spike） | `away == true` 时允许按日程自由移动；若实测发现仍被钳制，则改为 Transpiler 跳过年龄判断分支 |

`Child.update` 本身**先不动**：它已经调用 `NPC.update`，而 `NPC.update` 会处理 `followSchedule` / `checkSchedule`。P2 spike 的第一项就是验证"只补 tenMinuteUpdate + MovePosition"能否让日程正常跑通。

#### 日程与对话注入

- 资产键：`Characters/schedules/<nameKey>`、`Characters/Dialogue/<nameKey>`（实测原版用 `Characters\schedules\` 前缀）。
- `<nameKey>` = `NameKey.Normalize(child.Name)`：
  - 去首尾空格、去掉 `/` `\` `:` `*` `?` `"` `<` `>` `|` 等非法字符；
  - 中文保留（SMAPI 资产键允许非 ASCII）；
  - 与所有现有 NPC 名及另一个孩子的 key 比较，冲突则追加 `_2` 并在 `lwwc/nameKey` 记录；
  - 命名后**重建**资产（`InvalidateCache`），不能只在 `Entry` 里建一次字典。
- 日程内容按阶段生成：`4/5/6` 三个模板，雨天/节日模板回退到"留家"。
- 对话用 `Characters/Dialogue/<nameKey>` 的键值，配合 `lwwc/flags` 做条件分支。

#### 读档去重与兜底（DESIGN.md §8 的 B 方案风险）

- `SaveLoaded` / `ReturnedToTitle`：清空内存态，按 `lwwc/away` 重新摆放实体。
- `SaveLoaded` 校验：若 `away=true` 但孩子在 `FarmHouse.characters` 里 → 说明存档时状态不一致，**强制回家**（宁可丢一天上学，不让孩子卡两处）。
- 出门前强制检查：孩子不在任何其它 `location.characters` 里（`Utility.ForEachLocation` 扫一遍），有则先移除。
- 节日/婚礼/剧情：`DayStarted` 检测 `Game1.isFestival()` 或 `Game1.CurrentEvent != null` → 今天不外出。

#### 降级预案（方案 A）

接口抽象成：

```csharp
interface IChildPresenceStrategy
{
    void SendToTown(Child child, string location);
    void ReturnHome(Child child);
    bool IsAway(Child child);
}
```

- `SingleEntityPresence`（方案 B，默认）
- `ProxyNpcPresence`（方案 A：隐藏真孩子、生成同名 `NPC`、回家销毁）

切换只改 `ModEntry` 里注册的实现，上层（能力、对话、事件）完全无感。这也让 P2 spike 失败时的回退成本可控。

### 7.3 孩子能力（对应 DESIGN.md §3 的"玩家收益"）

统一接口，避免每个阶段写一套散代码：

```csharp
public interface IChildAbility
{
    string Id { get; }                       // "water_crops" 等
    int MinStage { get; }                    // 解锁阶段
    bool CanRun(Child child, Farmer player);  // 条件：在家/在外、天气、是否已做
    void Run(Child child, Farmer player);
    string Describe();                       // 日志/对话用
}
```

执行策略：

- 每个游戏日**只执行一次**（`lwwc/workDoneToday`），在 `ChildWorkHour` 触发。
- 能力注册表按 `MinStage` 排序，取当前阶段**最高优先级且可执行**的一个（避免同一天浇水又收蛋，收益叠加失控）。
- 数值全部进 `docs/balance.md` + `StageTable`，代码里不写魔法数字。
- 执行前检查 `Game1.CurrentEvent == null`、玩家不在 cutscene、目标位置有作物/动物。

四个能力的技术要点：

| 能力 | 实现要点 |
|---|---|
| 浇水 | 扫 `Game1.getFarm().objects`，挑 `HoeDirt.state == 0` 且未浇水的格，最多 N 格；调 `HoeDirt.Water()`（等价于浇水壶逻辑） |
| 收蛋 | 扫 `Game1.getFarm().animals` 或 `objects` 里的 `Egg` 类产出；`animal.currentProduce` 处理 |
| 收成熟作物 | 只收 `readyForHarvest` 的 `HoeDirt.crop`，走 `harvest` 路径，产物进玩家背包（或家庭箱） |
| 代售 | **不真卖**：把产物放进"待售"列表，在 `DayEnding` 按当日价格结算，避免破坏价格波动；折价系数可配 |

### 7.4 伴侣周一 Keg 代收（对应 DESIGN.md §6.1）

两段式实现：

1. **表演**：`Characters/schedules/Abigail` 增量插入周一 8:00–9:00 的农场点位（只追加，不覆盖整表）。日程点位的 `end` 行为触发结算。
2. **结算**：`KegCollector.CollectAll()`：
   - 遍历 `Game1.locations`，对每个 `location.objects.Pairs` 里 `obj is Keg`（或 `QualifiedItemId == "(BC)12"`）且 `readyForHarvest.Value == true` 且 `heldObject.Value != null` 的实例；
   - 找到家庭箱（`lwwc/familyChest` 标记的 `Chest`）；
   - `chest.GetItemsForPlayer().Add(held)`，成功则清空 `heldObject`、`readyForHarvest=false`、`MinutesUntilReady=0`；
   - 家庭箱满 / 不存在 → 保留桶内产物，写 `lwwc/familyChestFull`，次日对话提示。
3. **不依赖玩家在场**：结算在日程到点时执行，但若玩家当天不在农场（地图未加载），则退化为 `DayEnding` 全局扫描一次（用 `Utility.ForEachLocation` 强制加载农场）。

开关 `EnableSpouseCollection` 关闭时，只跳过注入不跳过已有日程清理。

### 7.5 家庭箱（对应 DESIGN.md §6.2）

**先做降级方案，再做 spike**：

- v1：原版 `Chest` + `modData["lwwc/familyChest"]="true"` 标记。玩家用普通箱子即可，掉落/放置逻辑零改动。
- 识别：`FamilyChest.Find()` 扫 `Game1.getFarm().objects` 找带标记的 `Chest`。
- 外观增强（可选）：用 `AssetRequested` 给带标记箱子的贴图叠加一张贴纸（`EditImage`），**不改容器逻辑**。
- 自定义 `BigCraftable` → 真 `Chest` 的 spike 放到 P5 之后，不作为发布阻塞项。若 SPIKE 成功再替换物品定义与放置逻辑（`placementAction` 里移除自身、生成 `Chest`、继承 modData）。

### 7.6 事件与对话

- 事件走 `Data/Events/<location>` 的 `AssetRequested` 增量追加，key 用 `lwwc_<id>` 前缀避免与其它 mod 冲突。
- 触发条件用 `mailReceived` flag（设计稿 §8 已定）+ `lwwc/flags` 双写：`mailReceived` 让原版事件系统能判断，`lwwc/flags` 用于我们自己的分支。
- 事件播放失败（前置 NPC 不在场等）：捕获异常 → 把 id 塞进 `pendingEvents` → 次日重试一次。
- 所有对话文本进 `i18n`，代码里只放 key。

### 7.7 i18n 与命名

- `i18n/default.json`（英文）+ `i18n/zh-CN.json`（中文），键名 `dialogue.stage4.first_day` 这种层级式。
- 玩家输入的孩子名**不进 i18n**，只进资产键（见 §7.2 的 `NameKey`）。
- `manifest.json` 不声明语言，SMAPI 按玩家语言自动回退 default。

---

## 8. Harmony 补丁清单

| 目标 | 签名 | 类型 | 用途 | 风险 | 阶段 |
|---|---|---|---|---|---|
| `Child.dayUpdate` | `void dayUpdate(int dayOfMonth)` | Prefix | 写入合成 `daysOld`，接管 7 天/级 | 中：`daysOld` 可见性待确认 | P1 |
| `Child.dayUpdate` | 同上 | Postfix（备选 A2） | 强制 `Age`/`speed` | 中：位置重排时机会偏 | P1 备选 |
| `Child.tenMinuteUpdate` | `void tenMinuteUpdate()` | Prefix | `away` 时禁止屋内乱走/回床 | 低 | P2 |
| `Child.MovePosition` | `void MovePosition(GameTime, Rectangle, GameLocation)` | Prefix（待 spike） | 放行日程移动 | 中：466 条 IL，需实测 | P2 |
| `Child.update` | `void update(GameTime, GameLocation)` | **暂不补** | 已内部调用 `NPC.update` | — | — |
| `NPC.get_IsVillager` | — | **不补** | `Child` 已重写为 true，够用 | — | — |
| `NPC.draw` | — | 不补 | 阶段 3 后沿用儿童外观，无需切换精灵 | — | — |

补丁类统一模板：

```csharp
[HarmonyPatch(typeof(Child), nameof(Child.dayUpdate))]
internal static class ChildAgePatch
{
    private static bool Prefix(Child __instance, int dayOfMonth)
    {
        try { AgingOverride.Apply(__instance); return true; }
        catch (Exception ex) { Log.Error("ChildAgePatch failed", ex); return true; }
    }
}
```

- `return true` 永远放行原版逻辑：我们只改输入，不阻断。
- 任何补丁异常都必须吞掉并降级（关闭对应功能），不能把异常抛回游戏主循环。

---

## 9. 资产与内容键规范

| 资产 | 键 | 编辑方式 | 备注 |
|---|---|---|---|
| 日程 | `Characters/schedules/<nameKey>` | 字典**新增键** | 只加 `spring_*` / `default` / `rain` 等必要键 |
| 对话 | `Characters/Dialogue/<nameKey>` | 字典新增键 | 键名 `lwwc/*` 前缀 |
| 阿比盖尔日程 | `Characters/schedules/Abigail` | 字典**增量**追加周一键 | 用 `EditData`，键冲突检测 |
| 事件 | `Data/Events/<location>` | 字典追加 | key `lwwc_<id>` |
| 家庭箱物品 | `Data/BigCraftables` | 追加条目 | id `LWWC.FamilyChest` |
| 物品贴图 | `Maps/springobjects` 或独立图 | `EditImage` | 仅在自定义外观时用 |
| 剧情邮件 | `Data/mail` | 追加 | flag 用 |

命名规范：

- 物品 ID：`{ModUniqueIdWithoutAuthor}.FamilyChest`（1.6 要求带命名空间，避免 `(O)` 冲突）。
- 事件 key / 邮件 key：`lwwc_` 前缀。
- 对话 key：`lwwc.` 前缀。
- 所有动态键生成后必须 `InvalidateCache`，且写入 `lwwc/nameKey` 以便下次启动复用。

---

## 10. 存档与兼容

| 场景 | 处理 |
|---|---|
| 卸 mod | 所有状态在 `modData`，原版忽略未知键；孩子回到原版行为，年龄段停在投影值（最多 55 天等价），不会坏档 |
| 中途装 mod | `SaveMigration` 按 `daysOld` 折算阶段，给一次提示 |
| 老存档已到儿童 | 折算为 stage 3，`daysInStage=0`，从 7 天重新计 |
| 多人 | `Context.IsMultiplayer` 时禁用成长推进与代收，`Log.Alert` 提示一次 |
| 冲突 mod | 启动时 `helper.ModRegistry.GetAll()` 扫描已知的小孩 mod（Child to NPC、Immersive Family 等），命中则 `EnableGrowth=false` 并提示 |
| 存档备份 | 依赖玩家自装 SaveBackup（本机已装）；发布说明里建议装 |
| 回退 | 每个阶段变更前写 `lwwc/prevStage`，`lwwc_rollback` 指令可回退一级（开发用） |

---

## 11. 里程碑技术拆解

| 阶段 | 技术任务 | 验收 |
|---|---|---|
| **P0** | 补 net6 ref pack；csproj + manifest + 空 `ModEntry`；`DeployMod` 目标；`[LWWC] loaded` 日志；`lwwc_info` 指令 | SMAPI 控制台显示加载、指令可用、无警告 |
| **P0.5（spike，1 天）** | ① 方案 B 最小可行：把成年孩子从 `FarmHouse.characters` 移到镇上、`TryLoadSchedule`、观察寻路/存档/读档 | 能走日程、读档 3 次无重复实体；**结论决定 P2 走 B 还是 A** |
| **P1** | `ChildState` + `SaveMigration` + `ChildAgePatch`；7 天/级；调试指令 | 新档 42 天到成年；老档折算正确；`lwwc_stage` 立即生效 |
| **P2** | `CommuteService` + `ScheduleBuilder` + `NameKey` + 两个 Child 补丁；读档去重 | 早 8 出、晚 5 回；中文/空格名可上学；读档 3 次不重复；节日/雨天留家 |
| **P3** | `IChildAbility` + 4 个能力 + `StageTable` 数值 | 每天有可见收益；关闭开关后无动作 |
| **P4** | 伴侣联动：阶段变更触发对话/事件；`pendingEvents` 顺延 | 每次进阶都有对应内容，且可重复测试 |
| **P5** | `SpouseSchedule` + `KegCollector` + `FamilyChest` | 满好感周一 8 点她走农场；Keg 成熟产出进箱；满箱有提示 |
| **P6** | i18n 补全、GMCM、平衡表、打包 zip | 打包可分发；中英切换正常 |

**P0.5 是关键路径**：spike 失败会直接改变 P2 的实现形态，所以必须在 P1 之前做，而不是等成长系统做完（DESIGN.md §9 已指出）。

---

## 12. 测试与调试

### 12.1 控制台指令

| 指令 | 作用 |
|---|---|
| `lwwc_info` | 打印所有孩子的阶段/天数/`Age`/`away`/位置/`nameKey` |
| `lwwc_stage <n>` | 强制阶段（顺带刷新 `Age`、日程、能力） |
| `lwwc_days <n>` | 设置 `daysInStage` |
| `lwwc_away <true/false>` | 强制离家/回家 |
| `lwwc_event <id>` | 强制触发事件 |
| `lwwc_keg` | 立刻执行一次代收 |
| `lwwc_chest` | 在玩家脚下生成家庭箱 |
| `lwwc_flags` | 打印 flag 与 pending 事件 |

全部通过 `helper.ConsoleCommands.Add` 注册，权限为 `ConsoleCommands` 默认（仅本机控制台）。

### 12.2 回归清单（`docs/test-plan.md`）

1. 新档 → 42 天 → 阶段 6；中间每个阶段都有对话与能力。
2. 老存档（孩子已 55+ 天）读档 → 阶段 3，提示一次。
3. 上学日出门 → 存档 → 读档 ×3 → 镇上只有一个孩子实体。
4. 中文名、带空格名、与村民重名 → 均能正常上学且有冲突提示。
5. 雨天 / 节日 → 不出门，行为回退到阶段 3。
6. 家庭箱满 → 代收不丢物品，次日有提示。
7. 关闭 `EnableSpouseCollection` → 周一日程恢复原版。
8. 多人模式 → 功能禁用且有提示。
9. 装/卸 mod 后存档可读。
10. 与 ContentPatcher 共存 → 资产编辑不互相覆盖。

### 12.3 日志规范

- 统一 `[LWWC]` 前缀。
- `Trace`：每日结算、状态写入。
- `Info`：阶段变更、出门/回家、代收结果。
- `Warn`：补丁降级、资产键冲突、家庭箱满。
- `Error`：异常（带堆栈），但必须已被 catch。

---

## 13. 风险登记（技术向）

| 风险 | 概率 | 影响 | 对策 |
|---|---|---|---|
| 拿不到 6.0 ref pack，net6 编不过 | 高 | 阻塞 P0 | 方案 A（离线包）/方案 B（netstandard2.1 过渡） |
| `daysOld` 不可写（可见性） | 中 | 成长接管方案变化 | A2 postfix 兜底；P1 第一天验证 |
| 只补 `tenMinuteUpdate`+`MovePosition` 不够，孩子出门后仍被拉回 | 中 | P2 返工 | P0.5 spike 先验证；失败切方案 A（代理 NPC） |
| `MovePosition` 466 条 IL 的 Transpiler 太脆 | 中 | 维护成本 | 优先 Prefix 放行；不改写指令流 |
| 孩子名做资产键的冲突/非法字符 | 中 | 上学失败 | `NameKey` 规范化 + 冲突后缀 + 失败降级留家 |
| 与 Child to NPC 类 mod 双实体 | 中 | 存档错乱 | 启动扫描 + 禁用自身成长 |
| 家庭箱自定义容器做不出来 | 中 | 功能缩水 | v1 直接走"原版箱子 + modData 标记" |
| 周一 Keg 代收被玩家误解为丢东西 | 低 | 口碑 | 只动成熟产出、对话说明、可关闭 |
| 补丁异常导致游戏崩 | 低 | 严重 | 全补丁 try/catch + 降级开关 |

---

## 14. 待决问题

- [ ] `Child.daysOld` 是否 public 可写？（P1 验证，决定 §7.1 方案）
- [ ] `Child.MovePosition` 在 `away` 状态下是否放行？（P0.5/P2 spike）
- [ ] `Age = 3` 的 `Child` 加入别的 `location.characters` 后，`FarmHouse.updateEvenIfFarmerIsntHere` 是否会把它拉回？（P0.5 spike）
- [ ] 家庭箱物品 ID 的最终命名（发布后不可改）
- [ ] `UniqueID` / `Author` 定名
- [ ] 能力数值表（对应 DESIGN.md §10）
- [ ] 代售折价系数与是否计入每日结算

---

## 15. 附录

### 15.1 版本锁定

| 组件 | 版本 |
|---|---|
| Stardew Valley | 1.6.15.24356 |
| SMAPI | 4.2.1 |
| Harmony（SMAPI 内置） | 2.2.2.0 |
| .NET（游戏运行时） | 6.0.32（游戏自包含） |
| C# | 10 |
| 目标框架 | net6.0 |

### 15.2 调试工具

- **ILSpy / dnSpy**：读 `Stardew Valley.dll` 原版逻辑（本方案 §1.2、§1.3 的事实都来自这里）。
- **SMAPI 控制台**：实时看 `[LWWC]` 日志与 mod 加载警告。
- **SaveBackup**：本机已装，回归测试前先备份。
- **`Stardew Valley.xml`**（游戏目录自带）：有部分 API 注释，可与反编译对照。

### 15.3 与 DESIGN.md 的对应关系

| DESIGN.md 章节 | 本方案章节 |
|---|---|
| §3 年龄阶梯 / §3.1 成长节奏 | §7.1、§6.3 |
| §4 学堂 | §7.2 |
| §5.1 工程约束 | §1、§2、§3 |
| §5.2 目录结构 | §4 |
| §5.3 出门方案 B | §7.2 |
| §5.4 内容层 | §2.4、§9 |
| §6 伴侣内容 | §7.4、§7.5 |
| §7 存档兼容 | §6.4、§10 |
| §8 风险 | §13 |
| §9 里程碑 | §11 |
| §10 待定 | §6.2、§14 |

---

# 16. v1.1 技术变更（2026-11-13）

> 本章**取代** §6.2 的伴侣配置项、§7.2 的"改写 Penny 日程"部分、§7.4、§7.5 的获取方式、§9 的 Abigail 日程行。
> 产品依据见 DESIGN.md v0.6（§3.2 校历、§4.2 课时、§4.3 座位、§4.4 天气节日、§6.1 节日陪伴、§6.2 收桶）。

## 16.0 删除清单

| 对象 | 处理 | 原因 |
|---|---|---|
| `Commute/TeacherService.cs` | **删除** | 不再改写 Penny 的日程（v1.0 的两个 bug 都出在这里） |
| `Spouse/SpouseSchedule.cs` | **删除** | 伴侣巡场取消 |
| `Spouse/SpouseService.cs` 的周一收桶部分 | **删除** | 收桶改由孩子做 |
| `Spouse/ScheduleSplicer.cs` | **删除** | 唯一使用者（老师/伴侣注入）都没了 |
| `Core/ScheduleGuard.cs` | **删除** | 同上，熔断器是为注入准备的 |
| 配置 `EnableSpouseCollection` / `EnableSpouseFarmWalk` / `EnableTeacherSchedule` | **删除** | 对应功能取消 |
| i18n `hud.keg.*` / `config.spouse_*` | 改写 | 语义从"伴侣代收"变成"孩子收桶" |
| 资产编辑 `Characters/schedules/Abigail` | **删除** | 不再改任何 NPC 的日程（只读探测） |

保留：`Spouse/StageScenes.cs`（阶段场景与两个分支选择）、`Family/FamilyChest.cs`、`Patches/ObjectPlacementPatch.cs`。

## 16.1 校历与课时探测

新增 `Commute/Timetable.cs`：

```csharp
internal readonly struct ClassSession
{
    internal readonly string Location;   // 课堂所在图
    internal readonly int StartTime;     // 她到课堂的时间
    internal readonly int EndTime;       // 她下一条日程的时间（上限 17:00）
}

internal static class Timetable
{
    internal static bool IsClassDay(Child child, ChildState state);
    internal static ClassSession? Probe(NPC teacher);   // 只读
}
```

**IsClassDay**（全部满足才算有课）：

```
季节 ∈ { spring, fall }
&& Game1.Date.DayOfWeek ∈ { Tuesday, Wednesday, Friday }
&& !Utility.isFestivalDay()
&& !Game1.isRaining && !Game1.isGreenRain
&& NameKey.CanUseAssetKeys(child, out _)
```

**Probe** 算法（关键：只读，不改她的任何资产）：

1. 候选课堂：`ArchaeologyHouse`、`CommunityCenter`（常量表，便于以后加）。
2. `teacher.Schedule == null` 时调一次 `teacher.TryLoadSchedule()`（原版加载自己的日程，安全）。
3. 遍历 `teacher.Schedule`（`Dictionary<int, SchedulePathDescription>`）：找 `desc.targetLocationName` 落在候选里的条目，取**时间最小**的一条 → `start`。
4. `end` = 所有 `time > start` 的条目里最小的时间；没有则 `start + 600`；上限 `1700`。
5. 没有匹配条目 → 返回 null（今天没课，孩子留家）。

**排课**（沿用 `ScheduleBuilder`，只换参数）：

```
{ depart } { school } { seatX } { seatY } 2 / { end } FarmHouse { homeX } { homeY } 2
depart = max(800, start - 60)
```

仍然必须先写 `child.DefaultPosition = child.Position`（原版解析以它为起点，`Child` 默认 (0,0) 会直接抛异常）。

> **为什么删掉注入**：v1.0 的注入产生过两类故障（同一时间点重复导致 `Dictionary.Add` 抛异常；在 `AssetRequested` 内调用 `parseMasterSchedule` 重入，把 NPC 的 master schedule 缓存污染成 null）。原版 Penny 本来就会去上课，注入是纯粹的额外风险。

## 16.2 课堂座位

新增 `Commute/ClassroomSeat.cs`：

```csharp
internal static class ClassroomSeat
{
    // 返回座位格；找不到返回 null
    internal static Vector2? Find(GameLocation location, Vector2 teacherTile);
}
```

**算法（产品规则：离她最近的桌子旁边的空格）**：

1. 收集桌子：`location.furniture` 中 `f.IsTable()` 的家具，取其占地矩形（`f.TileLocation` + `f.getTilesWide()/getTilesHigh()`）。
2. 对每张桌子，枚举沿桌沿向外一格的相邻格。
3. 过滤：`location.isTileOnMap(tile)`、`location.isTilePassable(tile)`、`!location.objects.ContainsKey(tile)`、该格没有家具占位、没有角色站立。
4. 在剩余候选里取 `Vector2.Distance(tile, teacherTile)` 最小的一格。
5. **没有桌子时的兜底**：`SchoolLocator.FindOpenTile` 在 `teacherTile` 周围找最近可站格。

**座位锁定**（`Commute/SeatLock.cs` 或并入 `CommuteService`）：

- 上课窗口 `[StartTime, EndTime)` 内，对每个在课堂的孩子：
  - 不在座位格 → 用 `PathFindController` 走过去；失败或超时（3 次）→ `setTileLocation(seat)` 直接落位，绝不卡死。
  - 已在座位格 → `Halt()`、`faceDirection(朝向老师)`、`Sprite.StopAnimation()`（静止即"坐着"的近似）；每次 tick 重新校准朝向。
- 下课：解除锁定，日程带他回家。
- 上课期间不响应"跟随玩家"，也不进 `tenMinuteUpdate` 的屋内游走（在外本来就跳过）。

**坐姿近似（方案 A）**：位置 + 朝向 + 静止。
预留 `ModConfig.SeatedSprite`（默认空）：配置后把 `child.Sprite` 换成该纹理并固定帧，用于将来接一张 16×32 的儿童坐姿图，不改逻辑。

> **已验证的硬约束**：`Furniture.sittingFarmers` 与 `MapSeat.sittingFarmers` 的键都是 `Farmer.UniqueMultiplayerID`，`Character`/`NPC` 没有任何 sit 状态，`Characters\Toddler` 也没有坐姿帧 —— 所以"真坐"必须自带图。

## 16.3 节日（v1.1.1：取消自己实现，交给原版）

**结论：本 mod 不做任何节日特判。**

反编译确认（StardewValley.Event）：

- 节日里原版会遍历**玩家配偶的孩子**，child.Age >= 3 的孩子会被**复制成事件演员**（EventActor = true），
  位置算在伴侣旁边，并 walkInSquare(3, 3, 2000) 让他在附近活动；
- 也就是说"孩子节日跟在阿比盖尔身边"**是原版自带行为**。

v1.1.0 曾自己实现过 Spouse/FestivalCompanion.cs（传送进临时地图 + 打开 showWorldCharacters + armerPassesThrough），
那会在原版的孩子演员之外**再放入一个真实孩子**，等于现场出现两个孩子。**已删除**，同时删除配置 EnableFestivalCompanion
与 armerPassesThrough 相关清理逻辑。

本 mod 在节日只做两件事：

1. 校历里**不排课**（Game1.isFestival() / Utility.isFestivalDay() 优先于一切）；
2. **不做周末家务**（KegChore 在 Game1.isFestival() 时直接返回）。

"节日当天孩子在家还是在场"完全由原版决定，不需要我们操心，也不会被存档迁移打断。
## 16.4 周末收桶（取代 §7.4）

新增 `Family/KegChore.cs`，状态写 `child.modData`：

| 键 | 含义 |
|---|---|
| `lwwc/chore/day` | 这是哪一天（`Date.TotalDays`），跨天自动重置 |
| `lwwc/chore/state` | `idle` / `walking` / `done` |
| `lwwc/chore/target` | 目标桶格 `x,y` |
| `lwwc/chore/skipped` | 当天放弃的桶（`x,y` 逗号分隔） |
| `lwwc/chore/count` | 今天已收数量 |

**启动**：`DayOfWeek ∈ {Sat, Sun}` 且 `timeOfDay ≥ 900` 且阶段 ≥ 5 且非节日 且家庭箱存在 且 `EnableWeekendKegChore`。

**每 10 分钟 tick**：

1. `idle`：扫 `Farm.objects` 里 `(BC)12` + `readyForHarvest` + `heldObject != null` + 不在 `skipped` 的桶 → 取离孩子最近的；
   找它四邻中可站的一格（找不到 → 记入 `skipped`，换下一个）；
   `child.controller = new PathFindController(child, farm, tile, facing, onArrive)`；`state = walking`、记录 `target`。
2. `walking`：控制器还在跑就等；控制器为 null 或超过 3 个 tick 没到达 → 记入 `skipped`，回 `idle`。
3. 到达（`onArrive` 回调）：`FamilyChest.TryAdd(chest, held)` 成功 → 清空桶（`heldObject=null/readyForHarvest=false/MinutesUntilReady=0`）、`count++`、播放动作（`Sprite` 2~3 帧挥手动效）+ `"coin"` 音效；失败（箱子满）→ `state = done` + HUD 提示。
4. `done` / `count ≥ MaxKegsPerDay` / `timeOfDay ≥ 1700`：收工 → 汇总 HUD（"收好了 N 个桶"），状态回 `idle` 并记 `day`，当天不再启动。

**玩家不在农场**：`Game1.currentLocation != farm` 时**暂停**（不派新任务、不推进判定），玩家回到农场继续；当天没做完留到下个周末。
（理由：非当前地图的 NPC 不更新，静默批量完成会违背"要有动作"的产品要求。）

**范围**：只处理 `Farm` 上的桶；棚屋/姜岛等其它地图一律不碰（同样是为了"看得见"）。

## 16.5 家庭箱来源变更

容器实现不变（真 `Chest` + `lwwc/familyChest` 标记 + `Object.placementAction` 补丁）。
**获取方式**：孩子升到阶段 5 的那天 `DayStarted`，若家里还没有家庭箱 → 赠送一个并提示（替代原"伴侣满好感赠送"）。`KeyChestGiven` 仍写在玩家 `modData`。

## 16.6 配置与 i18n 变更

```csharp
// 新增
EnableLesson            = true;   // 有课才上学
EnableSeat              = true;   // 上课锁定座位
EnableWeekendKegChore   = true;   // 周末收桶
KegChoreStartHour       = 9;
MaxKegsPerDay           = 40;
SeatedSprite            = "";     // 预留：儿童坐姿图资产名

// 删除
// EnableSpouseCollection / EnableSpouseFarmWalk / EnableTeacherSchedule
```

i18n：新增 `hud.lesson.*`、`hud.chore.*`、`dialogue.in_class`；改写 `school.*`（改成"今天没课"等）、`config.*`（删伴侣两项、加新项）；删除 `hud.keg.*`。

## 16.7 诊断指令（只读）

| 指令 | 输出 |
|---|---|
| `lwwc_schedule <NPC名>` | 该 NPC 当天已解析日程的 `时间 → 目的地 → 目标格`，用于核对 Penny 的课时/课堂地点 |
| `lwwc_classroom` | 当前课堂地图的桌子清单、候选座位、老师位置、最终选中的座位 |

两条都不改任何状态。

## 16.8 回归要点（增量，追加到 §12.2）

1. 春秋的周二/三/五：孩子 8 点左右出门、坐在桌旁、下课前不游走、17:00 前回家。
2. 春秋的周一/周四、整个夏天和冬天：孩子留家。
3. 雨天/雷雨/绿雨：留家（即使当天是二/三/五）。
4. 节日：现场能看到孩子站在伴侣旁边；节日结束后孩子在农舍。
5. 周六/周日：孩子逐个走到桶边、有可见动作、产物进家庭箱。
6. 家庭箱满：不再继续收、有提示、桶里产物一件不少。
7. 玩家离开农场：收桶暂停；回到农场继续。
8. 上课时段/收桶中途存档再读档：状态正确，无重复实体。
9. `lwwc_schedule Penny` 能打出一份人能看懂的课时表。

## 16.9 风险与降级

| 风险 | 降级 |
|---|---|
| Penny 的课堂目标名不在候选列表（被 mod 改了） | `lwwc_schedule Penny` 核对 → 把目标名加进候选；否则今天没课 |
| 课堂里没有桌子 | 座位退化为"她旁边的空格" |
| 节日事件期间打开 `showWorldCharacters` 后多出角色 | 临时地图通常没有其它世界角色；若观察到异常则改为"把孩子做成事件演员"（风险更高，不首选） |
| 收桶时找不到相邻可站格 | 该桶记入 `skipped`，跳过并记日志 |
| 控制器卡住 | 3 个 tick 未到达即放弃该桶 |
| 玩家全程不在农场 | 当天不收；下个周末继续（不静默完成） |