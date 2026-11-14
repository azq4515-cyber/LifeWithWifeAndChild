using System;
using StardewValley;
using StardewValley.Objects;
using LifeWithWifeAndChild.Core;

namespace LifeWithWifeAndChild.Family
{
    /// <summary>
    /// Keg（酿酒桶）代收（DESIGN.md §6.1 / TECH-DESIGN §7.4）。
    ///
    /// 1.6 里没有 <c>Keg</c> 这个类型——机器是数据驱动的，Keg 是 BigCraftable <c>(BC)12</c>。
    /// 所以判定就是：限定 id == (BC)12 且 <c>readyForHarvest</c> 且 <c>heldObject != null</c>。
    /// 只动成熟产出，未完成的不碰；产物原样搬运（品质/数量/实例都不改）。
    /// </summary>
    internal static class KegCollector
    {
        internal const string KegQualifiedId = "(BC)12";

        internal sealed class Result
        {
            internal int Moved;
            internal int Blocked;
            internal bool NoChest;
            internal int KegsScanned;
        }

        internal static Result CollectAll()
        {
            var result = new Result();

            try
            {
                var chest = FamilyChest.Find();
                if (chest == null)
                {
                    result.NoChest = true;
                    ModLog.Warn("keg: 没找到家庭箱，本次不代收。");
                    return result;
                }

                var locations = Game1.locations;
                if (locations == null) return result;

                foreach (var location in locations)
                {
                    if (location?.objects == null) continue;

                    // 先快照 key，避免边遍历边改集合
                    var keys = new System.Collections.Generic.List<Microsoft.Xna.Framework.Vector2>();

                    try
                    {
                        foreach (var pair in location.objects.Pairs)
                        {
                            if (pair.Value != null && pair.Value.QualifiedItemId == KegQualifiedId)
                                keys.Add(pair.Key);
                        }
                    }
                    catch (Exception ex)
                    {
                        ModLog.Error("keg: 扫描 '" + location.NameOrUniqueName + "' 失败。", ex);
                        continue;
                    }

                    foreach (var key in keys)
                    {
                        if (!location.objects.TryGetValue(key, out var obj) || obj == null) continue;
                        if (obj.QualifiedItemId != KegQualifiedId) continue;

                        result.KegsScanned++;

                        try
                        {
                            if (!obj.readyForHarvest.Value) continue;

                            var held = obj.heldObject.Value;
                            if (held == null) continue;

                            if (!FamilyChest.TryAdd(chest, held))
                            {
                                result.Blocked++;
                                continue;
                            }

                            obj.heldObject.Value = null;
                            obj.readyForHarvest.Value = false;
                            obj.MinutesUntilReady = 0;
                            result.Moved++;
                        }
                        catch (Exception ex)
                        {
                            ModLog.Error("keg: 处理 " + key + " 失败。", ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModLog.Error("KegCollector.CollectAll failed.", ex);
            }

            return result;
        }
    }
}
