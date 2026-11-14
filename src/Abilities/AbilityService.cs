using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using LifeWithWifeAndChild.Core;

namespace LifeWithWifeAndChild.Abilities
{
    /// <summary>
    /// 每日能力调度（TECH-DESIGN §7.3）。
    /// 与文档的一处有意偏离：文档说"每天只跑一个能力"，实现里改成"跑当前阶段解锁的**全部**能力"。
    /// 理由：成年阶段的设计定位是"农场副手（代售 + 自动农活）"，互相覆盖会让高级阶段变弱；
    /// 每个能力都有独立上限（MaxWaterTiles / MaxHarvestCrops / MaxSellItems），收益不会失控。
    /// </summary>
    internal static class AbilityService
    {
        private static readonly IChildAbility[] All =
        {
            new WaterCropsAbility(),
            new CollectProduceAbility(),
            new HarvestCropsAbility(),
            new SellProduceAbility(),
        };

        private static readonly Dictionary<string, IChildAbility> ById = new Dictionary<string, IChildAbility>(StringComparer.OrdinalIgnoreCase);

        static AbilityService()
        {
            foreach (var ability in All) ById[ability.Id] = ability;
        }

        internal static IChildAbility? Find(string id)
        {
            return id != null && ById.TryGetValue(id, out var ability) ? ability : null;
        }

        /// <summary>到点/回家时调用；每个孩子的能力每天最多结算一次。</summary>
        internal static void RunDailyWork()
        {
            if (!Context.IsMainPlayer) return;
            if (!ModEntry.Config.EnableChildWork) return;

            try
            {
                if (Game1.CurrentEvent != null) return; // cutscene 里不动世界
                if (Game1.player == null) return;

                foreach (var child in ChildRegistry.All())
                {
                    var state = ChildState.Get(child);

                    if (!state.IsManaged) continue;
                    if (state.WorkDoneToday) continue;
                    if (Commute.CommuteService.IsAway(child)) continue; // 在外面上学时先干活不合逻辑

                    state.WorkDoneToday = true;
                    RunFor(child, state);
                }
            }
            catch (Exception ex)
            {
                ModLog.Error("AbilityService.RunDailyWork failed.", ex);
            }
        }

        private static void RunFor(Child child, ChildState state)
        {
            var player = Game1.player;

            foreach (var id in StageTable.Abilities(state.Stage))
            {
                var ability = Find(id);
                if (ability == null) continue;

                try
                {
                    if (!ability.CanRun(child, player, state)) continue;

                    var count = ability.Run(child, player, state);
                    if (count <= 0) continue;

                    ModLog.Info($"ability: '{child.Name}' {ability.Id} -> {count}.");
                    Game1.addHUDMessage(new HUDMessage(ability.Describe(count), HUDMessage.newQuest_type));
                }
                catch (Exception ex)
                {
                    ModLog.Error("ability '" + id + "' failed for '" + child.Name + "'.", ex);
                }
            }
        }
    }
}
