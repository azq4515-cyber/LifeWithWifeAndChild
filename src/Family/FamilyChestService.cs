using System;
using StardewModdingAPI;
using StardewValley;
using LifeWithWifeAndChild.Core;

namespace LifeWithWifeAndChild.Family
{
    /// <summary>
    /// 家庭箱的发放（DESIGN.md §6.3）。
    /// v1.1 起不再挂在"伴侣满好感"，改成**孩子升到阶段 5、第一次要干活时**家里给一个。
    /// </summary>
    internal static class FamilyChestService
    {
        internal static void OnDayStarted()
        {
            if (!Context.IsMainPlayer) return;

            try
            {
                if (Game1.player == null) return;
                if (Game1.player.modData.GetBool(Constants.KeyChestGiven)) return;
                if (FamilyChest.Find() != null) return;

                // 至少要有一个到达阶段 5 的孩子
                var hasWorker = false;
                foreach (var child in ChildRegistry.All())
                {
                    var state = ChildState.Get(child);
                    if (state.IsManaged && state.Stage >= Stage.Teen) { hasWorker = true; break; }
                }

                if (!hasWorker) return;

                Game1.player.modData.SetBool(Constants.KeyChestGiven, true);

                if (!FamilyChest.GiveItemToPlayer()) return;

                Game1.addHUDMessage(new HUDMessage(I18n.Get("hud.chest.gifted_teen"), HUDMessage.newQuest_type));
                ModLog.Info("chest: 家庭箱已赠送给玩家（孩子到阶段 5）。");
            }
            catch (Exception ex)
            {
                ModLog.Error("FamilyChestService.OnDayStarted failed.", ex);
            }
        }
    }
}
