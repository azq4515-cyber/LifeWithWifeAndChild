using System;
using StardewModdingAPI;
using StardewValley;

namespace LifeWithWifeAndChild.Core
{
    /// <summary>i18n 之外的两个小工具：判断键是否存在、安全取当前伴侣。</summary>
    internal static class I18nExtensions
    {
        internal static bool Exists(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;

            try
            {
                return ModEntry.Helper.Translation.ContainsKey(key);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>取玩家伴侣 NPC；没有/出错都返回 null。</summary>
        internal static NPC? SafeSpouse()
        {
            try
            {
                if (Game1.player == null) return null;
                return Game1.player.getSpouse();
            }
            catch (Exception ex)
            {
                ModLog.Error("SafeSpouse failed.", ex);
                return null;
            }
        }

        /// <summary>玩家此刻能不能被弹对话（不在事件/菜单/已有对话里）。</summary>
        internal static bool PlayerIsFree()
        {
            try
            {
                if (!Context.IsMainPlayer) return false;
                if (Game1.player == null) return false;
                if (Game1.eventUp || Game1.CurrentEvent != null) return false;
                if (Game1.activeClickableMenu != null) return false;
                if (Game1.dialogueUp) return false;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
