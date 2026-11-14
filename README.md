# Life with Wife and Child

星露谷（Stardew Valley 1.6.15 / SMAPI 4.2.1）模组：让**孩子真正长大** —— 从婴儿一路长成能帮你干活的成年人；让**伴侣对孩子的每一个成长阶段做出反应**。

不是"再做一个孩子 mod"，也不是"再做一个老婆 mod"，而是把两条线咬合在一起：**孩子的每个成长阶段，都会点亮伴侣的一部分内容**。

> 当前状态：**v1.1.1**（校历跟随 Penny 的真实课时、课堂有座位、孩子周末收桶）。
> 设计见 [DESIGN.md](DESIGN.md) v0.6，实现记录与偏差见 [docs/milestones.md](docs/milestones.md)，
> 逐条回归清单见 [docs/test-plan.md](docs/test-plan.md)。

---

## 它做了什么

### 七级成长轴（0–6）

| 阶段 | 名称 | 位置 | 你能拿到的收益 | 事件 |
|---|---|---|---|---|
| 0 | 婴儿 | 婴儿床 | — | 出生 |
| 1 | 学步 | 农舍 | 跟着你走、捡东西 | 第一次走路 |
| 2 | 幼儿 | 农舍 | 偶尔送小礼物 | — |
| 3 | 儿童 | 农舍 | **替你浇 8 格地**；节日跟着伴侣 | 童年 |
| 4 | 学龄 | 农舍 ↔ 课堂 | **收鸡蛋等畜舍产出** | 第一天上学的选择 |
| 5 | 少年 | 课堂 + 农场 | **收成熟作物**；**周末收酿酒桶** | 梦想 / 叛逆分支 |
| 6 | 成年 | 农场 | **代售 + 农活 + 周末收桶** | 成年礼 |

- 全阶段统一 **7 天/级**（含原版 0–3，共 42 天 ≈ 一季半），可配置；推进在**睡觉结算**时发生。
- 阶段 3 之后沿用儿童外观（v1 的显式取舍）。

### 上课：跟着 Penny 的真实课时

- **春秋两季的周二 / 周三 / 周五**有课；其余日子（含整个夏冬、春秋的周一/周四）**没课，孩子留家**。
- 雨 / 雷雨 / 绿雨停课；雪天照常。
- 课时不是写死的：读 Penny **当天日程**里她去课堂的那一条——她几点到就是几点上课，她下一条日程就是下课。
- **孩子有座位**：坐在**离她最近的桌子旁边的空格**，上课期间面朝她、不乱走、不挡路。
- 不修改 Penny 的任何日程，也不新建地图（她本来就会去上课）。

### 节日：原版自带，本 mod 不干预

- 节日当天孩子**本来就会**跟在伴侣身边 —— 这是原版的节日演员机制（`Event` 会把 Age ≥ 3 的孩子作为事件演员放到伴侣旁边）。
- 本 mod 在节日只做两件事：**不排课、不做周末家务**。

### 周末家务：收酿酒桶（要有动作）

- **周六 / 周日**，阶段 5 起，孩子会**真的走过去**：走到桶旁 → 播动作 → 把产物搬进**家庭箱** → 下一个。
- 只收**农场上的**、成熟并装着东西的酿酒桶（`(BC)12`）；罐头瓶、压酪机一律不碰。
- 家庭箱满了就停下提示，产物留在桶里；玩家不在农场时暂停，回来继续。
- 每天上限 40 个（可配置），17:00 收工。

### 家庭箱（新物品）

- 孩子升到阶段 5 时赠送一次；或控制台 `lwwc_chest` 生成。
- 放下后是**真正的原版箱子**（能右键打开、能存档），带 `modData` 标记 —— 没有自造容器类，卸 mod 也不会坏档。
- 外观复用原版箱子贴图，零美术资源。

---

## 安装（玩家）

1. 装 **SMAPI 4.0+**（开发机为 4.2.1）。
2. 需要 **Stardew Valley 1.6.15+**。
3. 到 [Releases](../../releases) 下载最新的 `LifeWithWifeAndChild-<版本>.zip`。
4. 解压，把里面的 **`LifeWithWifeAndChild` 文件夹整个**放进游戏的 `Mods\`（不要只放 dll）。
5. 启动游戏，SMAPI 控制台应看到 `[LWWC] loaded v1.1.1`。

> 装好后 `manifest.json` 里已带 `UpdateKeys`，SMAPI 会通过 GitHub Releases 检查更新。

可选：装 **Generic Mod Config Menu** 可以在游戏内调配置（不装也能改 `config.json`）。

---

## 构建（开发）

前置：**.NET SDK**（本机 8.0 即可）+ 游戏根目录在仓库上两级。

```powershell
cd Mods\LifeWithWifeAndChild
dotnet build
```

- 目标框架 `net6.0`，手写 csproj，DLL 用 `HintPath` 引用游戏目录、`Private=false`（不复制、不打包）。
- `AfterBuild` 只把 dll/pdb 拷到**仓库根**——仓库根本身就是 mod 目录（`manifest.json` 在这里），SMAPI 只加载一份。
- 不开 `PublishSingleFile` / `ReadyToRun`。

调试循环：改代码 → `dotnet build` → 启动游戏 → 在 SMAPI 控制台过滤 `[LWWC]`。

## 打包发布

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\package.ps1
```

产物：`dist\LifeWithWifeAndChild-<版本>.zip`，结构是

```
LifeWithWifeAndChild/
├─ manifest.json
├─ LifeWithWifeAndChild.dll
├─ README.md
└─ i18n/{default,zh-CN}.json
```

脚本会 `dotnet build -c Release`，然后校验 zip 里必须有 `manifest.json` / dll / i18n，且路径用正斜杠
（`Compress-Archive` 在 PowerShell 5.1 上会写出反斜杠路径，部分解压工具会解成怪名字，所以这里手写条目）。

发布流程：跑脚本 → 在 GitHub 建一个 tag 为 `v<版本>` 的 Release → 把 zip 拖进去。

---

## 配置（`config.json`，游戏内可改）

| 键 | 默认 | 说明 |
|---|---|---|
| `GrowthSpeed` | 7 | 每级天数（1–60） |
| `EnableChildWork` | true | 孩子日常家务（浇水/收产出/收作物/代售） |
| `EnableLesson` | true | 上课（春秋二/三/五） |
| `EnableSeat` | true | 上课坐好、面朝老师 |
| `EnableWeekendKegChore` | true | 周末收桶 |
| `KegChoreStartHour` | 9 | 收桶开工时刻 |
| `MaxKegsPerDay` | 40 | 每天最多收几个桶 |
| `ChildWorkHour` | 12 | 日常家务的时刻 |
| `MaxWaterTiles` | 8 | 每天最多浇几格 |
| `MaxHarvestCrops` | 10 | 每天最多收几株 |
| `MaxSellItems` | 10 | 每天最多代售几组 |
| `SellPriceMultiplier` | 0.9 | 代售折价 |
| `SeatedSprite` | 空 | 预留：儿童坐姿图资产名（有图就能从"近似"升级为"真坐"） |
| `EnableDebugCommands` | true | 注册 `lwwc_*` 指令 |
| `VerboseLogging` | false | 详细日志 |

数值依据见 [docs/balance.md](docs/balance.md)。

---

## 控制台指令（调试）

状态与操作：`lwwc_info` / `lwwc_stage <0-6>` / `lwwc_days <n>` / `lwwc_away <true|false>` / `lwwc_event <id>` / `lwwc_flags` / `lwwc_keg` / `lwwc_chore` / `lwwc_chest` / `lwwc_work` / `lwwc_rollback` / `lwwc_reload`

诊断（只读，用来核对课时/座位）：`lwwc_schedule <NPC名>` / `lwwc_classroom`

搭测试场景（不用等 42 天）：

| 指令 | 作用 |
|---|---|
| `lwwc_baby [名字] [female]` | 在农舍生成一个阶段 0 的孩子 |
| `lwwc_hearts <0-14>` | 把配偶好感设为指定心数 |
| `lwwc_day <1-28>` | 改当月日期（1/8/15/22 是周一，2/9/16/23 是周二…） |
| `lwwc_kegs <n>` | 在周围生成 n 个「已成熟、装着酒」的 Keg |

五分钟冒烟流程见 [docs/test-plan.md](docs/test-plan.md) §0。

---

## 存档与兼容

- 全部状态写在 `modData` 的 `lwwc/*` 键里，**不新增任何序列化类型** → 卸掉 mod 存档照常能读。
- 老存档里已经长大的孩子按原版真实阈值（13/27/55 天）折算阶段，并提示一次。
- 阶段推进前会校验；每天结算前把孩子强制带回农舍，保证"存档时孩子一定在家"。
- **不修改任何原版 NPC 的日程**（v1.0 的注入方案已删除）。
- v1 **不支持**多人联机（检测到会禁用成长/上课/收桶并提示）。
- 启动会扫描已知的小孩类 mod（Child to NPC / Immersive Family 等），命中则禁用成长，避免双实体。

---

## 文档

- [DESIGN.md](DESIGN.md) — 产品设计（v0.6：校历、座位、节日、收桶）
- [TECH-DESIGN.md](TECH-DESIGN.md) — 技术方案（v1.1 变更见 §16）
- [docs/milestones.md](docs/milestones.md) — 实现记录：做了什么、与方案的偏差、反编译确认的 API 事实
- [docs/balance.md](docs/balance.md) — 平衡数值表
- [docs/test-plan.md](docs/test-plan.md) — 手工回归清单

---

## 致谢

- [SMAPI](https://smapi.io/) by Pathoschild
- [Content Patcher](https://www.nexusmods.com/stardewvalley/mods/1915) by Pathoschild（本 mod 不依赖它，但保证共存）
