# 里程碑记录（实现侧）

> 这份文件记录**实际做出来的东西**、以及实现过程中对设计方案做的校正。
> 产品设计看 [DESIGN.md](../DESIGN.md)，技术方案看 [TECH-DESIGN.md](../TECH-DESIGN.md)。
> 二者是"当时的设计"，本文件是"最终的事实"——两者冲突时以本文件为准。

---

## 0. 一句话状态

P0–P6 全部落地并编译通过；mod 已在 SMAPI 4.2.1 / 星露谷 1.6.15 上**实测载入成功**、
四个 Harmony 补丁全部应用、无报错。纯逻辑（阶段换算 / 日程拼接 / 名字校验）有可执行的回归测试。
未做的是"在存档里逐天玩一遍"的实机回归，清单见 [test-plan.md](test-plan.md)。

---

## 1. 里程碑完成情况

| 里程碑 | 状态 | 产出 |
|---|---|---|
| P0 骨架 | ✅ | `LifeWithWifeAndChild.csproj`、`manifest.json`、`ModEntry`、`DeployMod`、`[LWWC] loaded` 日志、11 条调试指令 |
| P0.5 spike | ✅（读代码定案） | 方案 B 可行，见 §2 |
| P1 成长轴 0–6 | ✅ | `ChildState` / `SaveMigration` / `AgingOverride` / `GrowthService` / `ChildDayUpdatePatch` |
| P2 学龄出门 | ✅ | `CommuteService` / `ScheduleBuilder` / `SchoolLocator` / `TeacherService` / `NameKey` / 两个 Child 补丁 / 读档去重 |
| P3 阶段能力 | ✅ | `IChildAbility` + 浇水 / 收畜舍产出 / 收作物 / 代售 + `AbilityService` |
| P4 伴侣联动 | ✅ | `StageScenes`（含两个分支选择）+ 孩子分阶段台词 + `pendingEvents` 顺延 |
| P5 满好感家政 | ✅ | `FamilyChest` / `KegCollector` / `SpouseService` / `SpouseSchedule` / `ScheduleSplicer` / 家庭箱物品与放置补丁 |
| P6 收尾 | ✅ | `i18n/{default,zh-CN}`、GMCM 软依赖、`docs/balance.md`、`docs/test-plan.md`、本文件、README |

---

## 2. P0.5 spike 的结论（关键路径）

原方案要求"先验证方案 B 才能做成长系统"。实际做法是**读反编译代码定案 + 载入实测**，
结论是方案 B 成立，但成立的**真正原因**和原方案写的不一样：

| 原方案的假设 | 代码事实 | 影响 |
|---|---|---|
| `Child` 重写 `IsVillager`（暗示为 true） | **`public override bool IsVillager => false;`** | 孩子的日程**永远不会**被原版自动加载（`NPC.resetForNewDay` 里 `TryLoadSchedule()` 被 `if (IsVillager)` 挡住）。所以"出门"必须由我们主动 `TryLoadSchedule(key, raw)` —— 不是可选项，是唯一路径 |
| `Child.MovePosition` 466 IL，带年龄位置钳制，可能要 Transpiler | 它是一次**完整重写的四向移动**（Age 3 时四向都能走、有动画），没有任何年龄位置钳制 | **不需要** `MovePosition` 补丁。方案里"P2 待 spike 的第二项"直接消失，风险清单里"Transpiler 太脆"也随之作废 |
| 日程解析以孩子当前位置为起点 | 解析起点取的是 `DefaultMap` + `DefaultPosition`，而 `Child` 的 `defaultPosition` **默认是 (0,0)** | 推送日程前必须先把 `DefaultPosition` 写成孩子当前位置，否则原版直接抛 "start position … at tile (0, 0)"。这是"孩子能出门"的隐形前置条件，已在 `ScheduleBuilder` 里处理 |
| 原版 `FarmHouse.updateEvenIfFarmerIsntHere` 可能把孩子拉回 | 那里只处理**配偶**，完全不碰孩子 | 孩子出门后不会被自动拉回。真正的兜底点是 `Child.dayUpdate` / `Child.tenMinuteUpdate` 里对 `FarmHouse` 的硬转型（NRE 风险），已用补丁 + 回家兜底处理 |

---

## 3. 与设计/技术方案的偏差（以及为什么）

### 3.1 部署：仓库根目录就是 mod 目录

方案 §3.6 建议部署到 `Mods\_LWWC_dev\`。实际改为**把 dll 直接放到仓库根**
（`Mods\LifeWithWifeAndChild\`），因为：仓库根本来就有 `manifest.json`，
SMAPI 会递归扫描 `Mods\`，如果 dll 在别处，仓库根会变成一个"有 manifest 但没 dll"的坏 mod，
每次启动都报一次加载失败。放在根上则恰好一份、零告警。

### 3.2 阶段事件：对话场景，不是 `Data/Events`

方案 §7.6 要求走 `Data/Events/<location>`。实现改成**多页对话场景**（`StageScenes`），原因有两条硬约束：

1. 事件脚本要按**演员名**引用角色，而孩子的名字是玩家输入的 —— 写死的事件资产无法表达；
2. v1 没有新美术，事件里也没有能演的"动作"。

用 `Game1.DrawDialogue` 多页对话 + `createQuestionDialogue` 可以在零格式风险下交付同样的东西：
每个阶段一段有台词、有情绪、有**分支选择**的场景（`first_school` 的上学选择、`dream` 的梦想/叛逆分支）。
`Data/Events` 的版本等有了美术资源再补，接口（`pendingEvents` + `lwwc_event` 指令）已经留好。

### 3.3 能力：跑全部，而不是"每天只跑一个"

方案 §7.3 说"每天取最高优先级的一个，避免收益叠加失控"。实现改成**当前阶段解锁的全部能力各跑一次**，因为：
成年阶段的定位是"农场副手（代售 + 自动农活）"，只跑一个反而让最高阶段比少年阶段更弱。
叠加风险用**每个能力独立上限**控制（浇水 8 格 / 收 10 株 / 卖 10 组），数值见 [balance.md](balance.md)。

### 3.4 代售的容器：出货箱，不是家庭箱

方案 §7.3 说"产物放进待售列表，`DayEnding` 结算"。实现选了**出货箱**作为目标容器：
家庭箱是伴侣 Keg 产出的存放处，孩子若自动清空它，会把玩家存的酒卖掉 —— 明显负体验。
出货箱里的东西玩家本来就是要卖的，孩子的价值是"**当天就卖**"（代价：折价 90%）。
实现上是真的把物品取走并加钱，所以晚上原版出货结算看到的是空箱子，**不存在重复结算**。
卖不出钱的物品（垃圾/任务物品）原地留给原版处理。

### 3.5 "不新增序列化类型"照做，但多用了几个 modData 键

方案 §6.1 的键表之外新增：`lwwc/prevStage`、`lwwc/scenesSeen`、`lwwc/chestGiven`。
全部是 `modData` 字符串键，卸 mod 后原版忽略未知键，不坏档。

### 3.6 家庭箱：原版箱子 + modData 标记 + 自定义物品

方案 §7.5 的降级路线成立并被采纳，但补上了"真正的物品"这一层：
`Data/BigCraftables` 追加 `LifeWithWifeAndChild.FamilyChest`（**复用原版箱子贴图**，零美术），
放置时用 Harmony 前缀把自身换成真 `Chest` 并打 `lwwc/familyChest` 标记。
这样"自定义物品"和"原版容器逻辑"两件事都拿到了，也没有自造容器类的存档风险。

获取方式选用了 DESIGN.md §10 里的"满好感赠送"选项：伴侣 14 心且家里还没有家庭箱时赠送一次。
数据格式风险为零，而且和 §6.1 的触发条件天然对齐。

---

## 4. 实现期的 API 校正（写代码前逐条反编译确认过）

| 事实 | 影响 |
|---|---|
| `daysOld` 是 `public readonly NetInt`（readonly 的是字段，不是值） | 可以直接 `child.daysOld.Value = x`，§7.1 的首选方案成立，**不需要** A2 的 postfix 兜底 |
| `Child.IsVillager => false` | 孩子的日程必须手动加载（见 §2） |
| `Child.dayUpdate` 里 Age 2/3 分支有 `(currentLocation as FarmHouse)` 硬转型 | 孩子在外时进 dayUpdate 会 NRE。补丁先归位再放行 |
| `Child.tenMinuteUpdate` 同样有 `farmHouse.characters.Contains(this)` | 在外时必须跳过（已补丁） |
| `Child.checkAction` 对 Age>=2 的孩子只做 friendship + emote，**不显示任何对话** | 不补丁就永远没有"阶段台词"，所以有了 `ChildTalkPatch` |
| 原版已经从不调 `Child.dayUpdate` 以外的 aging | 统一 7 天/级只能靠接管 `dayUpdate` 的输入（合成 daysOld），§7.1 的做法正确 |
| 1.6 没有 `Keg` 类型 | 机器是数据驱动的，Keg = `(BC)12`；判定用 `readyForHarvest` + `heldObject` |
| `Chest.addItem(Item)` 返回放不下的剩余（null = 全放进去） | 家庭箱用它可以自动处理堆叠与容量，不用自己算 |
| `ModDataDictionary` **不是** `IDictionary<string,string>`，而是 Netcode 的 `NetStringDictionary` | 扩展方法必须写在 `ModDataDictionary` 上（第一版就是在这里编译失败） |
| `IManifest` 在 `smapi-internal\SMAPI.Toolkit.CoreInterfaces.dll` | 要额外引用这个程序集（`Private=false`） |
| 本机 NuGet **已可达**，`.NET 8 SDK` 能直接 `restore` 出 net6.0 引用包 | TECH-DESIGN §3.1 的"P0 前置阻塞"不再存在，`net6.0` 一路编到底 |
| `Data/BigCraftables` 是 `Dictionary<string, BigCraftableData>` | 追加条目即可，字段为 `Name/DisplayName/Description/Price/Fragility/CanBePlaced{Outdoors,Indoors}/IsLamp/Texture/SpriteIndex/ContextTags/CustomFields` |

---

## 5. 首轮实机测试发现并修掉的问题（v1.0.1）

测试者是拿着**已有孩子的老存档**进的，所以三个问题都在第一分钟就暴露了，这是好事。

### 5.1 伴侣日程插入撞时间点 → 原版解析抛异常

现象：`[game] NPC 'Abigail' failed to parse master schedule 'marriage_Mon' ... ArgumentException: Key: 830`，
随后本 mod 打印"插入后的日程无法解析，已放弃"。

根因：Abigail 已婚日程本来就有 `830 SeedShop ...`，我们硬插了一条 `830 Farm ...`。
日程字典是**按时间做键**的，同一个时间两条 → `Dictionary.Add` 抛"键重复"，她一整天的日程都废掉。
（我们的验证环节救回来了，没有把坏日程写进资产，但红色报错已经刷到控制台。）

修复：
- `ScheduleSplicer.IsTimeUsed` + `TryPickFreeTime`：插入前先查重，锚点被占用就在 ±2 小时内找一个**合法的空时间点**
  （按 HHMM 规则进位，不会产生 `860` 这种非法时间），找不到就放弃巡场。
- `Insert` 遇到同一时间直接返回 null（防御），调用方改为**先拿到原始日程再拼指令**。
- 回归用例直接用了这次失败的原始字符串：必须挑到 840 且不产生重复键。

### 5.2 课堂落点永远找不到 → 学龄后根本出不了门

现象：`[LWWC] schedule: 在 'ArchaeologyHouse' 找不到可站立的格子，今天不上学。`

根因：我把 `location.warps[0].TargetX/TargetY` 当成了**本地图内的起点**。
实际上 warp 的两个坐标含义不同：`X/Y` 是本地图上的门，`TargetX/TargetY` 是**目标地图**的落点。
所以是在博物馆里从"镇上的坐标"开始做 BFS，起点不在地图上，`recursiveFindOpenTileForCharacter` 必然返回 Zero。

修复：
- 起点改用 `warp.X/Y`（门内一格），并依次尝试多个候选起点；
- 仍找不到就做整图扫描兜底（跳过有物体/有地形/有人站的格子，用 `isTilePassable` 判定），
  比"今天就上不了学"好；走了兜底会打一条 `Warn` 说明。

### 5.3 成年阶段还在上学（设计偏差，不是崩坏）

`Stage.GoesToSchool` 原来写成 `stage >= 4`，导致阶段 6（成年）每天还去博物馆。
按 DESIGN.md §3，阶段 6 的位置是"农场为主"，上学只属于阶段 4–5。已改为 `stage == 4 || stage == 5`。
顺带解决了一个误导性日志：`lwwc_stage 6` 后会打印"找不到可站立的格子，今天不上学"，现在成年根本不会去尝试。

### 5.4 顺带调整

- 周一代收的结算点从"8:00 窗口开头"挪到 **9:00**：她 8:30 左右才走去农场，9 点收的时候她人已经在场，表演和结算对得上。
- 新增 4 条测试用指令（`lwwc_baby` / `lwwc_hearts` / `lwwc_day` / `lwwc_kegs`），否则"没孩子的存档"根本进不了主流程。

### 5.5 确认正常的部分（这一轮日志里）

- `[LWWC] loaded` 能起来，补丁全部应用。
- 老存档迁移正确：`daysOld=133 → stage=3`、`36 → stage=2`、`109 → stage=3`、`38 → stage=2`。
- 家庭箱满好感赠送触发一次。
- 伴侣日程插入失败时**没有**污染她的日程（验证环节生效）。

### 5.6 老师日程注入：验证方式本身把 NPC 的日程缓存搞坏了（v1.0.2）

现象（实机）：

```
[game] Failed to load schedule 'Fri' for NPC 'Penny': GOTO references schedule 'Tue' which couldn't be parsed.
[game] NullReferenceException at NPC.parseMasterScheduleImpl ... line 6241
[game] NPC 'Penny' failed to parse master schedule 'Fri' with raw data 'GOTO Tue/900 ArchaeologyHouse 5 13 2'
[game] KeyNotFoundException: The schedule file for NPC 'Penny' could not be loaded...
```

两个独立的错，叠在一起：

**（1）往"纯转发"条目里插指令**
Penny 的很多日程键内容就是一行 `GOTO Tue`。我往这种条目后面插了一条 `900 ArchaeologyHouse ...`，
但原版解析器看到第一条是 `GOTO` 就直接转发去解析 `Tue`，我们插的那条**根本不会被读到**——
更糟的是它让 raw 变成 `GOTO Tue/900 ...` 这种"转发 + 垃圾尾巴"的形状。
修复：注入前先用 `getMasterScheduleRawData()` 把 `GOTO` 链解析到**最终那条具体日程**（最多 8 跳，
按原版规则处理 `GOTO season` 与回退 spring），插到那个键里；解析不到就完全不动。

**（2）在 AssetRequested 里调用原版解析器做验证 → 重入 → 缓存被写成 null**
`ScheduleSplicer.Verify` 里调了 `npc.parseMasterSchedule(...)`，而它内部会
`Game1.content.Load` **同一个正在被构建的资产**。重入的结果是
`getMasterScheduleRawData()` 拿到 null 并把 `_hasLoadedMasterScheduleData` 置为 true ——
于是这一天 Penny 的所有日程解析都失败（NRE → KeyNotFound），**她整天没有日程**。
这个验证比它要防的错更危险。

修复：
- 删掉 `Verify`，换成 `ScheduleSplicer.ValidateSplice`：**纯结构校验**，
  只检查条目数 +1、时间点不重复（新增那条也参与查重）、每条指令的时间段可解析。
  不碰任何游戏状态、不触发任何资产加载，因此不可能重入。
- 加 `ScheduleGuard`：按天的"当日熔断"。注入后再加载一次，
  如果 NPC 的 `Schedule` 变成 null（说明改坏了），就把该资产标记为今天别再动、
  清缓存并重载一次——回滚成原版，宁可少一天授课，也不能让 NPC 一整天没有行程。
- 伴侣巡场走的是同一套逻辑，同样从 `Verify` 换成了 `ValidateSplice` + 熔断回滚。

回归用例（用出问题的原始日程字符串）：

```
ok   ValidateSplice 合法拼接 -> true
ok   ValidateSplice 重复时间(第二条 830) -> false   [第一次写测试时这里漏了：新增条目没参与查重]
ok   ValidateSplice 丢条目 -> false
ok   ResolveFinalKey: 'GOTO Tue' 的键会插到 Tue 而不是转发键（人工核对）
```

> 注：这次也是**测试自己先错了一回**——第一版 `ValidateSplice` 把"新增的指令"从查重里 `continue` 掉了，
> 导致"插了第二条 830"这种正是要防的情况反而通过。测试失败暴露了它，已修正为新增条目也参与查重。

---

## 6. 已知限制（v1 明确不做）

1. **多人联机**：检测到就停用成长推进与代收并 `Alert` 提示（只保留调试指令）。
2. **与小孩类 mod 共存**：启动扫描到 `Child to NPC` / `Immersive Family` 一类 mod 时，禁用成长与上学。
3. **成年外观**：阶段 3 之后沿用儿童外观（DESIGN.md §3 的显式取舍），没有成人精灵。
4. **节日**：带孩子参加节日不支持；节日当天不上学。
5. **老师只处理 Penny**：`TeacherService` 固定找 `Penny`；把她娶了或加了替换她日程的 mod 时，
   插入会用原版解析器验证，验证不过就放弃（那天孩子照常去图书馆，只是没人讲课）。
6. **代售按 `sellToStorePrice()` 粗略计价**：不叠加"农耕家/工匠"等职业的出货加成，
   因为有折价系数兜底，方向上不会让玩家吃亏到离谱。

---

# 7. v1.1 实现记录（校历 / 座位 / 节日 / 收桶）

> 产品依据：DESIGN.md v0.6。技术细节：TECH-DESIGN.md §16。
> 这一版做的不是"加功能"，而是**把 v1.0 一条走错的路拆掉重铺**。

## 7.1 删掉的东西（比新增的更重要）

| 删除 | 原因 |
|---|---|
| `Commute/TeacherService.cs` | 不再改写 Penny 的日程。v1.0 的两个 bug（时间点撞车、`AssetRequested` 里重入解析污染缓存）都出自这里 |
| `Spouse/SpouseSchedule.cs` | 伴侣巡场日程注入取消 |
| `Spouse/SpouseService.cs` | 伴侣周一代收取消（连带 14 心触发逻辑） |
| `Spouse/ScheduleSplicer.cs` | 唯一使用者（老师/伴侣注入）都删了 |
| `Core/ScheduleGuard.cs` | 同上，当日熔断器是为注入准备的 |
| 配置 `EnableSpouseCollection` / `EnableSpouseFarmWalk` / `EnableTeacherSchedule` | 功能取消 |

净减约 600 行 + 两个已修过的 bug 类型。**v1.1 起这个 mod 不再修改任何原版 NPC 的日程**（只读探测）。

## 7.2 新增 / 重写

| 文件 | 作用 |
|---|---|
| `Commute/Timetable.cs` | 校历（春秋 + 二/三/五）+ 课时探测（读她的日程） |
| `Commute/ClassroomSeat.cs` | 座位：离她最近的桌子旁空格 |
| `Commute/SeatService.cs` | 上课锁定座位、面朝老师、按她的实际位置校正座位 |
| `Commute/ScheduleBuilder.cs` | 改为生成"上课日"日程（出门 → 座位 / 下课 → 家） |
| `Commute/CommuteService.cs` | 用课时替代"每天都上学"；兜底送达/送回 |
| `Family/KegChore.cs` | 周末收桶状态机（走 + 动作 + 进箱子 + 暂停 + 上限） |
| `Family/FamilyChestService.cs` | 家庭箱改为"孩子到阶段 5 时赠送" |
| `Spouse/FestivalCompanion.cs` | 节日把孩子带到伴侣旁边并保证可见 |
| `Core/ScheduleGuard.cs`（删）→ 不再需要 | — |

## 7.3 这一版确认的 API 事实

| 事实 | 出处 | 影响 |
|---|---|---|
| `WorldDate.GetDayOfWeekFor(day) = (DayOfWeek)(day % 7)`，`shortDayNameFromDayOfSeason` 同源 | 反编译 | 周几判定可放心用 `Game1.Date.DayOfWeek`；每月 1/8/15/22 是周一，2/9/16/23 是周二…… |
| 原版 `TryLoadSchedule` 对 Penny 硬编码 `Tue/Wed/Fri → marriageJob` | 反编译 | 印证"她的工作日就是二/三/五"，我们只需加入她的课 |
| `SchedulePathDescription` 有 `targetLocationName` / `time` | 反射 | 课时探测不需要猜，直接读她解析后的日程 |
| `Furniture.IsTable()` = `furniture_type` 5 或 11 | 反编译 | "桌子"是原版概念，不用自己认图 |
| `Furniture` / `MapSeat` 的座位都只认 `Farmer.UniqueMultiplayerID`，`Character` 没有 sit 状态 | 反编译 | **NPC 真坐姿做不到**，只能用"位置+朝向"近似（方案 A） |
| 节日 = 临时地图 + `Event`；村民是事件演员；`drawCharacters` 在 `eventUp && !showWorldCharacters` 时直接 return | 反编译 | 节日陪伴必须"传进临时地图 + 打开 showWorldCharacters" |
| `PathFindController(Character, GameLocation, Point, int)` 存在（4 参重载） | 反射 | 收桶/座位可直接用原版寻路 |

## 7.4 与设计的偏差

1. **座位的锚点在 DayStarted 时不可靠**：那时她还在家，只能拿课堂入口当锚点。
   实现上分两步：DayStarted 先用入口算一个临时座位写进日程；`SeatService` 在上课期间按她的**实际位置**重新算并校正。
2. **收桶时玩家不在农场 → 暂停**：非当前地图的 NPC 不更新，静默批量完成等于骗人；
   当天没做完留到下个周末。已经在 DESIGN §6.2 / TECH §16.4 写明。
3. **只收农场上的桶**：棚屋/姜岛不碰，因为"要有动作"要求孩子真的走过去。
4. **节日期间打开 `showWorldCharacters`** 会让"世界角色"参与绘制；节日临时地图里除孩子没有别的世界角色，
   所以副作用极小。若观察到异常，降级方案是"把孩子做成事件演员"（风险更高，不首选）。

## 7.5 v1.1 已知限制

1. **没有真坐姿**：需要一张 16×32 ×4 帧的儿童坐姿图；`ModConfig.SeatedSprite` 已预留。
2. **课堂必须有桌子**：没有桌子时座位退化为"她旁边的空格"，观感会差一些（用 `lwwc_classroom` 可查）。
3. **Penny 的课堂地点名写死在候选表**（`ArchaeologyHouse` / `CommunityCenter`）：若被别的 mod 改到别处，
   会变成"今天没课"（用 `lwwc_schedule Penny` 可核对）。
4. **大桶阵列一天收不完**：受 `MaxKegsPerDay`（默认 40）限制，剩下的留到下个周末。
5. 收桶顺序是"离孩子最近的优先"，不是按排布顺序；观感上可能来回走。

## 7.6 v1.1.1 修正：节日不要自己实现

**用户反馈**：孩子节日时本来就跟在阿比盖尔身边。核对后发现用户是对的。

反编译 `StardewValley.Event`（第 3336–3442 行）：

- 节日里原版会遍历**玩家配偶的孩子**；
- `child.Age >= 3` 的孩子会被**复制成事件演员**（`child2.EventActor = true`），
  位置算在伴侣旁边，并且 `walkInSquare(3, 3, 2000)` 让他在附近来回走。

所以 v1.1.0 的 `Spouse/FestivalCompanion.cs` 是**多余的**，而且有害：
我们往节日临时地图里塞了一个**真实孩子**，原版又放了一个**孩子演员**，现场会出现两个孩子。

处理：

- 删除 `Spouse/FestivalCompanion.cs`（约 120 行）；
- 删除 `ModEntry` 里的 `TimeChanged` / `Player.Warped` 钩子与两处 `Reset()`；
- 删除配置 `EnableFestivalCompanion` 与 GMCM 选项、i18n `config.festival.*`；
- 删除 `CommuteService.ForceReturnHome` 里为节日准备的 `farmerPassesThrough` 复位；
- 文档（DESIGN §6.1 / §5.1 事实表 / §5.2 目录 / 风险表 / 里程碑，TECH §16.3、README、balance、test-plan）同步改写为"节日交给原版"。

**这一版的教训**：先把原版行为读清楚，再决定要不要自己写。
"节日孩子跟着伴侣"和"Penny 上课"是同一类事情——原版都已经做了，我们只该**加入**，不该重做。