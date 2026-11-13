# Life with Wife and Child

星露谷（Stardew Valley 1.6.15 / SMAPI 4.2.1）模组：让**孩子真正长大**——从婴儿一路长成能帮你干活的成年人；让**伴侣阿比盖尔**对孩子的每一个成长阶段做出反应。

> 当前状态：**设计阶段**，功能代码尚未开始。

## 核心设计

- 年龄阶梯 0–6：原版 0–3，新增学龄、少年、成年
- 学龄起独立去镇上上学（博物馆/图书馆 → 社区中心），成年后留在农场当副手
- 孩子能帮玩家干活并有真实收益（浇水、收蛋、代售）
- 阿比盖尔的内容被孩子阶段"点亮"，两条线耦合而非并联

完整设计见 [DESIGN.md](DESIGN.md)。

## 构建

计划采用离线构建（本机 NuGet 不可用），手写 `csproj` 引用游戏根目录 DLL 与 `smapi-internal\0Harmony.dll`，
目标框架 `net6.0`。脚手架在 P0 阶段建立。

## 致谢

- [SMAPI](https://smapi.io/) by Pathoschild
- [Content Patcher](https://www.nexusmods.com/stardewvalley/mods/1915) by Pathoschild
