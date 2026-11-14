using System;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Objects;
using LifeWithWifeAndChild.Content;
using LifeWithWifeAndChild.Core;
using LifeWithWifeAndChild.Spouse;

namespace LifeWithWifeAndChild.Patches
{
    /// <summary>
    /// 让孩子真的能说话（DESIGN.md §3 的"新对话"）。
    ///
    /// 原版 <c>Child.checkAction</c> 对 Age>=2 的孩子只做 friendship + emote 就 return true，
    /// 永远不会走对话系统 —— 所以必须在这里接管，否则"每个阶段都有新台词"根本无从触发。
    ///
    /// 保持原版行为的分支：手上拿着帽子（原版给孩子戴帽子的功能）、婴儿/学步（Age&lt;2）、
    /// 今天已经聊过、以及未被本 mod 接管的孩子。
    /// </summary>
    [HarmonyPatch(typeof(Child), nameof(Child.checkAction))]
    internal static class ChildTalkPatch
    {
        private static bool Prefix(Child __instance, Farmer who, GameLocation l, ref bool __result)
        {
            try
            {
                if (__instance == null || who == null) return true;
                if (__instance.IsInvisible) return true;

                // 手上拿着帽子：交给原版（戴帽子/摘帽子）
                if (who.Items.Count > who.CurrentToolIndex && who.Items[who.CurrentToolIndex] is Hat)
                    return true;

                // 婴儿与学步阶段保持原版（这时候还不会说话）
                if (__instance.Age < 2) return true;

                var name = __instance.Name;
                if (string.IsNullOrEmpty(name)) return true;

                if (!who.friendshipData.ContainsKey(name))
                    who.friendshipData.Add(name, new Friendship(250));

                if (who.hasTalkedToFriendToday(name)) return true;

                var state = ChildState.Get(__instance);
                if (!state.IsManaged) return true;

                // 与原版一致的友谊/表情副作用
                who.talkToFriend(__instance);
                __instance.doEmote(20);
                __instance.faceTowardFarmerForPeriod(4000, 3, false, who);

                var key = StageScenes.DialogueKey(state);
                var text = DialogueContent.TryGetLine(__instance, key)
                           ?? I18n.Get(key, new { child = name });

                if (!string.IsNullOrWhiteSpace(text))
                    Game1.DrawDialogue(new Dialogue(__instance, key, text));

                __result = true;
                return false;
            }
            catch (Exception ex)
            {
                ModLog.Error("ChildTalkPatch failed; falling back to vanilla.", ex);
                return true;
            }
        }
    }
}
