using System;
using System.Collections.Generic;
using StardewValley;
using StardewValley.Characters;
using StardewValley.TerrainFeatures;
using LifeWithWifeAndChild.Core;

namespace LifeWithWifeAndChild.Abilities
{
    /// <summary>
    /// 阶段 5（少年）：替玩家收成熟作物。
    ///
    /// 走原版 <c>Crop.harvest</c>，不自己复制掉落逻辑：当 junimoHarvester 为 null 时它会把产物
    /// 直接塞进 <c>Game1.player</c> 背包（背包满则落地成 debris），并且正确处理
    /// 品质、额外掉落、再生作物（草莓/蓝莓）与收获后清地。这样我们不会污染原版产物逻辑。
    /// </summary>
    internal sealed class HarvestCropsAbility : IChildAbility
    {
        public string Id => "harvest_crops";

        public int MinStage => (int)Stage.Teen;

        public bool CanRun(Child child, Farmer player, ChildState state)
        {
            return ModEntry.Config.MaxHarvestCrops > 0 && Game1.getFarm() != null;
        }

        public int Run(Child child, Farmer player, ChildState state)
        {
            var farm = Game1.getFarm();
            if (farm == null) return 0;

            var limit = Math.Max(0, ModEntry.Config.MaxHarvestCrops);
            var targets = new List<KeyValuePair<Microsoft.Xna.Framework.Vector2, HoeDirt>>();

            try
            {
                foreach (var pair in farm.terrainFeatures.Pairs)
                {
                    if (targets.Count >= limit) break;
                    if (pair.Value is not HoeDirt dirt) continue;
                    if (dirt.crop == null) continue;
                    if (!dirt.readyForHarvest()) continue;

                    targets.Add(new KeyValuePair<Microsoft.Xna.Framework.Vector2, HoeDirt>(pair.Key, dirt));
                }
            }
            catch (Exception ex)
            {
                ModLog.Error("harvest_crops: 扫描作物失败。", ex);
                return 0;
            }

            var harvested = 0;

            foreach (var target in targets)
            {
                try
                {
                    var x = (int)target.Key.X;
                    var y = (int)target.Key.Y;

                    if (target.Value.crop == null) continue;
                    if (target.Value.crop.harvest(x, y, target.Value))
                        harvested++;
                }
                catch (Exception ex)
                {
                    ModLog.Error("harvest_crops: 收获失败 @" + target.Key + ".", ex);
                }
            }

            if (harvested > 0)
            {
                try { farm.playSound("harvest"); } catch { /* ignore */ }
            }

            return harvested;
        }

        public string Describe(int count)
        {
            return I18n.Get("hud.ability.harvest", new { count });
        }
    }
}
