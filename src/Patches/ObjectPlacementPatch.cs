using System;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewValley;
using LifeWithWifeAndChild.Core;
using LifeWithWifeAndChild.Family;
using SObject = StardewValley.Object;

namespace LifeWithWifeAndChild.Patches
{
    /// <summary>
    /// 家庭箱物品放置 → 生成真正的 <see cref="StardewValley.Objects.Chest"/>（TECH-DESIGN §7.5 的"自定义容器"spike）。
    ///
    /// 为什么要补这里：1.6 里"能打开的容器"只有 <c>Chest</c> 实体。给 BigCraftable 加一条数据只能得到
    /// 一个"摆件"，右键打不开。放下去的那一刻把自身换成真 Chest 并打 modData 标记，就同时拿到了
    /// "自定义物品"和"原版容器逻辑"两件事。
    ///
    /// 调用方（<c>Utility.tryToPlaceItem</c>）在 placementAction 返回 true 时会自己
    /// <c>reduceActiveItemByOne()</c>，所以这里不需要、也不应该再动玩家背包。
    /// </summary>
    [HarmonyPatch(typeof(SObject), nameof(SObject.placementAction))]
    internal static class ObjectPlacementPatch
    {
        private static bool Prefix(SObject __instance, GameLocation location, int x, int y, Farmer who, ref bool __result)
        {
            try
            {
                if (__instance == null || location == null) return true;
                if (__instance.QualifiedItemId != Constants.FamilyChestQualifiedId) return true;

                var tile = new Vector2(x / 64, y / 64);
                var chest = FamilyChest.CreateAt(location, tile);

                __result = chest != null;
                if (chest == null)
                    Game1.showRedMessage(I18n.Get("hud.chest.cannot_place"));

                return false;
            }
            catch (Exception ex)
            {
                ModLog.Error("ObjectPlacementPatch failed; falling back to vanilla.", ex);
                return true;
            }
        }
    }
}
