using System;
using HarmonyLib;
using StardewValley.Characters;
using LifeWithWifeAndChild.Core;

namespace LifeWithWifeAndChild.Patches
{
    /// <summary>
    /// 孩子在外时禁止"屋内乱走 / 回婴儿床"（TECH-DESIGN §8）。
    ///
    /// 原版 <c>Child.tenMinuteUpdate</c> 里有硬转型：
    /// <c>FarmHouse farmHouse = base.currentLocation as FarmHouse; farmHouse.characters.Contains(this)</c>
    /// —— 孩子不在农舍时这里会 NRE。为了不只靠"调用方恰好不会调用它"这种隐含假设，
    /// 在外状态下直接跳过。
    /// </summary>
    [HarmonyPatch(typeof(Child), nameof(Child.tenMinuteUpdate))]
    internal static class ChildTenMinutePatch
    {
        private static bool Prefix(Child __instance)
        {
            try
            {
                var state = ChildState.Get(__instance);
                if (state.IsManaged && !( __instance.currentLocation is StardewValley.Locations.FarmHouse))
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                ModLog.Error("ChildTenMinutePatch failed.", ex);
                return true;
            }
        }
    }
}
