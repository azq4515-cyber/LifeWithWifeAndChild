using System;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.TerrainFeatures;
using LifeWithWifeAndChild.Core;

namespace LifeWithWifeAndChild.Abilities
{
    /// <summary>
    /// 阶段 3（儿童）：替玩家浇 N 格地。
    /// 只浇"有作物且需要水、当前没浇"的地，等价于浇水壶逻辑（state=1），不动肥料与作物本身。
    /// </summary>
    internal sealed class WaterCropsAbility : IChildAbility
    {
        public string Id => "water_crops";

        public int MinStage => (int)Stage.Child;

        public bool CanRun(Child child, Farmer player, ChildState state)
        {
            return ModEntry.Config.MaxWaterTiles > 0 && Game1.getFarm() != null;
        }

        public int Run(Child child, Farmer player, ChildState state)
        {
            var farm = Game1.getFarm();
            if (farm == null) return 0;

            var limit = Math.Max(0, ModEntry.Config.MaxWaterTiles);
            var watered = 0;

            foreach (var pair in farm.terrainFeatures.Pairs)
            {
                if (watered >= limit) break;

                if (pair.Value is not HoeDirt dirt) continue;
                if (!dirt.needsWatering() || dirt.isWatered()) continue;

                try
                {
                    dirt.state.Value = HoeDirt.watered;
                    watered++;
                }
                catch (Exception ex)
                {
                    ModLog.Error("water_crops: failed at " + pair.Key + ".", ex);
                }
            }

            if (watered > 0)
            {
                try { farm.playSound("wateringCan"); } catch { /* 音效失败无所谓 */ }
            }

            return watered;
        }

        public string Describe(int count)
        {
            return I18n.Get("hud.ability.water", new { count });
        }
    }
}
