using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Objects;
using LifeWithWifeAndChild.Core;

namespace LifeWithWifeAndChild.Family
{
    /// <summary>
    /// 家庭箱（DESIGN.md §6.2）的 v1 实现：**原版 Chest + modData 标记**。
    ///
    /// 为什么不做自定义容器类：1.6 的箱子是 <c>Chest</c> 实体，物品栏、右键菜单、存档都由它提供。
    /// 走"自定义物品放置时生成真 Chest 并打标记"这条降级方案，功能完全等价、零存档风险，
    /// 也避免了自造容器在其它 mod / 卸装情况下的兼容问题。
    /// </summary>
    internal static class FamilyChest
    {
        internal static bool IsFamilyChest(Chest chest)
        {
            return chest?.modData != null && chest.modData.GetBool(Constants.KeyFamilyChest);
        }

        internal static void Mark(Chest chest)
        {
            if (chest?.modData == null) return;

            chest.modData.SetBool(Constants.KeyFamilyChest, true);
            try
            {
                chest.modData[Constants.KeyFamilyChestOwner] = (Game1.player?.UniqueMultiplayerID ?? 0L).ToString();
            }
            catch
            {
                // 只为将来多人留位，失败不影响功能
            }
        }

        /// <summary>在任意地图里找家庭箱（农场、农舍、小屋都算）。</summary>
        internal static Chest? Find()
        {
            try
            {
                var locations = Game1.locations;
                if (locations == null) return null;

                foreach (var location in locations)
                {
                    var chest = FindIn(location);
                    if (chest != null) return chest;
                }
            }
            catch (Exception ex)
            {
                ModLog.Error("FamilyChest.Find failed.", ex);
            }

            return null;
        }

        private static Chest? FindIn(GameLocation? location)
        {
            if (location?.objects == null) return null;

            try
            {
                foreach (var pair in location.objects.Pairs)
                {
                    if (pair.Value is Chest chest && IsFamilyChest(chest))
                        return chest;
                }
            }
            catch (Exception ex)
            {
                ModLog.Error("FamilyChest.FindIn failed.", ex);
            }

            return null;
        }

        /// <summary>放进家庭箱；放不进去（满了）返回 false，调用方必须保留原物品。</summary>
        internal static bool TryAdd(Chest chest, Item item)
        {
            if (chest == null || item == null) return false;

            try
            {
                if (chest.Items.Count >= chest.GetActualCapacity())
                {
                    // 还有可能叠到已有的同类堆里，交给 addItem 判断
                    var leftover = chest.addItem(item);
                    return leftover == null;
                }

                var rest = chest.addItem(item);
                return rest == null;
            }
            catch (Exception ex)
            {
                ModLog.Error("FamilyChest.TryAdd failed.", ex);
                return false;
            }
        }

        /// <summary>在指定位置生成一个家庭箱（物品放置 / 调试指令共用）。</summary>
        internal static Chest? CreateAt(GameLocation location, Microsoft.Xna.Framework.Vector2 tile)
        {
            if (location == null) return null;

            try
            {
                if (location.objects.ContainsKey(tile))
                {
                    ModLog.Warn("FamilyChest.CreateAt: 目标格子已被占用。");
                    return null;
                }

                var chest = new Chest(true);
                chest.TileLocation = tile;
                Mark(chest);
                location.objects.Add(tile, chest);
                location.playSound("axe");

                return chest;
            }
            catch (Exception ex)
            {
                ModLog.Error("FamilyChest.CreateAt failed.", ex);
                return null;
            }
        }

        /// <summary>把家庭箱物品塞进玩家背包（满好感赠送 / 调试指令）。</summary>
        internal static bool GiveItemToPlayer()
        {
            try
            {
                if (!ItemRegistry.Exists(Constants.FamilyChestQualifiedId))
                {
                    ModLog.Warn("FamilyChest: 物品定义不存在，无法发放（Data/BigCraftables 注入失败？）。");
                    return false;
                }

                var item = ItemRegistry.Create<StardewValley.Object>(Constants.FamilyChestQualifiedId);
                Game1.player.addItemByMenuIfNecessary(item);
                return true;
            }
            catch (Exception ex)
            {
                ModLog.Error("FamilyChest.GiveItemToPlayer failed.", ex);
                return false;
            }
        }

        /// <summary>统计家庭箱里物品的件数（调试/日志用）。</summary>
        internal static int CountItems(Chest chest)
        {
            if (chest?.Items == null) return 0;

            var count = 0;
            foreach (var item in chest.Items)
            {
                if (item != null) count += Math.Max(1, item.Stack);
            }

            return count;
        }

        internal static List<string> Describe()
        {
            var lines = new List<string>();
            var chest = Find();

            if (chest == null)
            {
                lines.Add("家庭箱: 未放置");
                return lines;
            }

            lines.Add($"家庭箱: {chest.Location?.NameOrUniqueName ?? "?"} @ {chest.TileLocation}, {chest.Items.Count}/{chest.GetActualCapacity()} 格, {CountItems(chest)} 件");
            return lines;
        }
    }
}
