using System;
using HarmonyLib;
using StardewValley.Characters;
using LifeWithWifeAndChild.Core;
using LifeWithWifeAndChild.Growth;

namespace LifeWithWifeAndChild.Patches
{
    /// <summary>
    /// 接管原版 aging（TECH-DESIGN §8 的第一条补丁）。
    ///
    /// 补丁只做"拦截与转发"：把 daysOld 写成合成值，然后**永远 return true** 放行原版，
    /// 这样原版的 resetForNewDay / 位置重排 / reloadSprite / speed 全部照原版跑。
    /// </summary>
    [HarmonyPatch(typeof(Child), nameof(Child.dayUpdate))]
    internal static class ChildDayUpdatePatch
    {
        private static bool Prefix(Child __instance)
        {
            try
            {
                return AgingOverride.Apply(__instance);
            }
            catch (Exception ex)
            {
                // 补丁绝不能把异常抛回游戏主循环
                ModLog.Error("ChildDayUpdatePatch failed; falling back to vanilla behavior.", ex);
                return true;
            }
        }
    }
}
