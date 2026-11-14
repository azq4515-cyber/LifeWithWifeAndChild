using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using LifeWithWifeAndChild.Core;

namespace LifeWithWifeAndChild.Growth
{
    /// <summary>
    /// 老存档折算与缺键修复（TECH-DESIGN §6.4）。
    /// 只在读档后与每天开始时跑，且迁移只跑一次（用 lwwc/migrated 标记）。
    /// </summary>
    internal static class SaveMigration
    {
        /// <summary>保证所有孩子都有合法的成长状态；返回被新接管的孩子数量。</summary>
        internal static int EnsureAll()
        {
            var migrated = 0;

            foreach (var child in ChildRegistry.All())
            {
                try
                {
                    if (Ensure(child)) migrated++;
                }
                catch (Exception ex)
                {
                    ModLog.Error("SaveMigration.EnsureAll failed for '" + child?.Name + "'.", ex);
                }
            }

            return migrated;
        }

        /// <summary>返回 true 表示这次刚刚做了迁移。</summary>
        private static bool Ensure(Child child)
        {
            if (child == null) return false;

            var state = ChildState.Get(child);

            if (state.IsManaged)
            {
                // 已有状态：只修越界值，不覆盖玩家里程碑
                var stage = state.Stage;
                state.ForceStage(stage);
                state.ApplyVanillaAge();
                return false;
            }

            // 老存档：按原版真实阈值 13/27/55 反推
            int daysOld;
            try
            {
                daysOld = child.daysOld.Value;
            }
            catch
            {
                daysOld = 0;
            }

            Stage mapped;
            if (daysOld < 0)
            {
                ModLog.Warn($"child '{child.Name}' has abnormal daysOld={daysOld}; resetting to stage 0.");
                mapped = Stage.Baby;
            }
            else if (daysOld > 3650)
            {
                ModLog.Warn($"child '{child.Name}' has abnormal daysOld={daysOld}; clamping to stage 3.");
                mapped = Stage.Child;
            }
            else
            {
                mapped = StageTable.FromVanillaDaysOld(daysOld);
            }

            state.ForceStage(mapped);
            state.DaysInStage = 0;
            state.LastGrowthDay = -1;
            state.MarkMigrated();
            state.ApplyVanillaAge();

            NotifyMigration(child, mapped);
            ModLog.Info($"migrated child '{child.Name}': daysOld={daysOld} -> stage={(int)mapped}.");
            return true;
        }

        private static void NotifyMigration(Child child, Stage stage)
        {
            try
            {
                if (!Context.IsMainPlayer) return;

                var flag = Constants.MailPrefix + "migrated";
                if (Game1.player.mailReceived.Contains(flag)) return;

                Game1.player.mailReceived.Add(flag);
                Game1.addHUDMessage(new HUDMessage(
                    I18n.Get("hud.migrated", new { child = child.Name, stage = I18n.Get(stage.NameKey()) }),
                    HUDMessage.newQuest_type));
            }
            catch (Exception ex)
            {
                ModLog.Error("NotifyMigration failed.", ex);
            }
        }

        /// <summary>把一个孩子的阶段直接刷新到游戏里（调试指令用）。</summary>
        internal static void RefreshChild(Child child)
        {
            var state = ChildState.Get(child);
            state.ApplyVanillaAge();
            Commute.CommuteService.RefreshToday(child);
        }

        /// <summary>列出所有孩子的状态摘要（调试输出用）。</summary>
        internal static List<string> Describe()
        {
            var lines = new List<string>();

            foreach (var child in ChildRegistry.All())
            {
                var state = ChildState.Get(child);
                lines.Add(string.Format(
                    "'{0}' stage={1}({2}) daysInStage={3} age={4} daysOld={5} away={6} loc={7} nameKey-readable={8}",
                    child.Name,
                    (int)state.Stage,
                    I18n.Get(state.Stage.NameKey()),
                    state.DaysInStage,
                    child.Age,
                    child.daysOld.Value,
                    state.Away,
                    ChildRegistry.DescribeLocation(child),
                    NameKey.IsUsable(NameKey.Resolve(child), out _)));
            }

            if (lines.Count == 0) lines.Add("(还没有孩子)");
            return lines;
        }
    }
}
