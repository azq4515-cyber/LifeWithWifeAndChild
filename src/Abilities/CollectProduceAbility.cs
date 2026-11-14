using System;
using System.Collections.Generic;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Locations;
using StardewValley.Objects;
using LifeWithWifeAndChild.Core;

namespace LifeWithWifeAndChild.Abilities
{
    /// <summary>
    /// 阶段 4（学龄）：收畜舍里掉出来的产物（鸡蛋 / 牛奶 / 羊毛……）。
    ///
    /// 依据（1.6 反编译确认）：HarvestType 为 DropOvernight 的动物在 <c>FarmAnimal.dayUpdate</c> 里
    /// 通过 <c>Utility.spawnObjectAround</c> 把产物掉在畜舍里，标记 <c>IsSpawnedObject=true</c>。
    /// 所以"收蛋"就是扫畜舍里带这个标记的 Object，不需要碰动物的产蛋逻辑。
    /// 顺带收农场上的同类掉落（松露等），产出一律进玩家背包。
    /// </summary>
    internal sealed class CollectProduceAbility : IChildAbility
    {
        private const int MaxItemsPerDay = 40;

        public string Id => "collect_produce";

        public int MinStage => (int)Stage.School;

        public bool CanRun(Child child, Farmer player, ChildState state)
        {
            return Game1.locations != null;
        }

        public int Run(Child child, Farmer player, ChildState state)
        {
            var collected = 0;

            foreach (var location in Game1.locations)
            {
                if (collected >= MaxItemsPerDay) break;
                if (location == null) continue;
                if (location is not AnimalHouse && !(location is Farm)) continue;

                try
                {
                    collected += CollectFrom(location, Math.Min(MaxItemsPerDay - collected, MaxItemsPerDay));
                }
                catch (Exception ex)
                {
                    ModLog.Error("collect_produce: '" + location.NameOrUniqueName + "' failed.", ex);
                }
            }

            return collected;
        }

        private static int CollectFrom(GameLocation location, int limit)
        {
            var keys = new List<Microsoft.Xna.Framework.Vector2>();

            try
            {
                foreach (var pair in location.objects.Pairs)
                {
                    var obj = pair.Value;
                    if (obj == null) continue;
                    if (!obj.IsSpawnedObject) continue;
                    if (obj is Chest) continue;
                    keys.Add(pair.Key);
                }
            }
            catch (Exception ex)
            {
                ModLog.Error("collect_produce: 扫描 objects 失败。", ex);
                return 0;
            }

            var count = 0;

            foreach (var key in keys)
            {
                if (count >= limit) break;
                if (!location.objects.TryGetValue(key, out var obj) || obj == null) continue;

                try
                {
                    if (!Game1.player.addItemToInventoryBool(obj)) break; // 背包满了就停手
                    location.objects.Remove(key);
                    count++;
                }
                catch (Exception ex)
                {
                    ModLog.Error("collect_produce: 收取失败 @" + key + ".", ex);
                    break;
                }
            }

            if (count > 0)
            {
                try { location.playSound("coin"); } catch { /* ignore */ }
            }

            return count;
        }

        public string Describe(int count)
        {
            return I18n.Get("hud.ability.collect", new { count });
        }
    }
}
