using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using LifeWithWifeAndChild.Abilities;
using LifeWithWifeAndChild.Commute;
using LifeWithWifeAndChild.Core;
using LifeWithWifeAndChild.Family;
using LifeWithWifeAndChild.Growth;
using LifeWithWifeAndChild.Spouse;

namespace LifeWithWifeAndChild.Commands
{
    /// <summary>
    /// 全部控制台指令（TECH-DESIGN §12.1 / §16.7）。每个指令都必须能在"世界已加载"之外安全报错。
    /// </summary>
    internal static class DebugCommands
    {
        internal static void Register(IModHelper helper)
        {
            helper.ConsoleCommands.Add("lwwc_info", "打印所有孩子的阶段/天数/Age/上课/位置、家庭箱、开关。", (_, _) => WithWorld(PrintInfo));
            helper.ConsoleCommands.Add("lwwc_stage", "强制设置阶段：lwwc_stage <0-6>", (_, args) => WithWorld(() => SetStage(args)));
            helper.ConsoleCommands.Add("lwwc_days", "设置当前阶段已过天数：lwwc_days <n>", (_, args) => WithWorld(() => SetDays(args)));
            helper.ConsoleCommands.Add("lwwc_away", "强制送到座位 / 强制回家：lwwc_away <true|false>", (_, args) => WithWorld(() => SetAway(args)));
            helper.ConsoleCommands.Add("lwwc_event", "强制播放一个阶段场景：lwwc_event <first_walk|childhood|first_school|dream|adulthood>", (_, args) => WithWorld(() => PlayEvent(args)));
            helper.ConsoleCommands.Add("lwwc_flags", "打印剧情 flag 与待播场景。", (_, _) => WithWorld(PrintFlags));
            helper.ConsoleCommands.Add("lwwc_keg", "立刻批量把成熟的酿酒桶收进家庭箱（不看动作，测试用）。", (_, _) => WithWorld(CollectKegs));
            helper.ConsoleCommands.Add("lwwc_chore", "立刻跑一轮「走过去收桶」家务（不受周末/时段限制）。", (_, _) => WithWorld(RunChore));
            helper.ConsoleCommands.Add("lwwc_chest", "在玩家脚下生成一个家庭箱。", (_, _) => WithWorld(CreateChest));
            helper.ConsoleCommands.Add("lwwc_work", "立刻结算一次孩子能力。", (_, _) => WithWorld(ForceWork));
            helper.ConsoleCommands.Add("lwwc_rollback", "把第一个孩子回退一级（开发用）。", (_, _) => WithWorld(Rollback));
            helper.ConsoleCommands.Add("lwwc_reload", "丢弃资产缓存并重建孩子的日程/对话。", (_, _) => WithWorld(ReloadAssets));
            helper.ConsoleCommands.Add("lwwc_schedule", "打印某 NPC 今天的「时间 -> 目的地」：lwwc_schedule <名字>", (_, args) => WithWorld(() => PrintSchedule(args)));
            helper.ConsoleCommands.Add("lwwc_classroom", "打印课堂的桌子/候选座位/最终座位。", (_, _) => WithWorld(PrintClassroom));

            // 测试场景搭建（lwwc_baby / lwwc_hearts / lwwc_day / lwwc_kegs）
            TestSetupCommands.Register(helper);
        }

        private static void WithWorld(Action action)
        {
            if (!Context.IsWorldReady)
            {
                Log("世界还没加载，这个指令需要先进入存档。");
                return;
            }

            try
            {
                action();
            }
            catch (Exception ex)
            {
                ModLog.Error("debug command failed.", ex);
                Log("指令执行失败：" + ex.Message);
            }
        }

        private static void Log(string message)
        {
            ModLog.Info(message);
        }

        // ---- 指令实现 ----

        private static void PrintInfo()
        {
            Log("=== LWWC 状态 ===");
            foreach (var line in GrowthService.Describe()) Log(line);
            foreach (var line in FamilyChest.Describe()) Log(line);

            Log($"今天: {Game1.currentSeason} {Game1.dayOfMonth} ({Game1.Date.DayOfWeek}), {Game1.timeOfDay}, 雨={Game1.isRaining} 绿雨={Game1.isGreenRain} 节日={Game1.isFestival()}");

            foreach (var child in ChildRegistry.All())
            {
                var state = ChildState.Get(child);
                var lesson = state.LessonLocation.Length > 0
                    ? $"{state.LessonLocation} {state.LessonStart}-{state.LessonEnd} 座位={state.LessonSeat}"
                    : "今天没课";
                Log($"'{child.Name}' 上课: {lesson}; 家务: {state.ChoreState} 已收 {state.ChoreCount}");
            }

            Log($"开关: work={ModEntry.Config.EnableChildWork} lesson={ModEntry.Config.EnableLesson} seat={ModEntry.Config.EnableSeat} chore={ModEntry.Config.EnableWeekendKegChore} 阻塞={ModEntry.FeaturesBlocked}");
            foreach (var line in Integrations.ModConflictDetector.Describe()) Log(line);
        }

        private static void SetStage(string[] args)
        {
            if (!TryGetChild(out var child)) return;

            if (args.Length < 1 || !int.TryParse(args[0], out var value))
            {
                Log("用法: lwwc_stage <0-6>");
                return;
            }

            GrowthService.SetStage(child, StageExtensions.Clamp(value));
            Log($"'{child.Name}' 现在是阶段 {value}（{I18n.Get(StageExtensions.Clamp(value).NameKey())}）。");
        }

        private static void SetDays(string[] args)
        {
            if (!TryGetChild(out var child)) return;

            if (args.Length < 1 || !int.TryParse(args[0], out var value))
            {
                Log("用法: lwwc_days <n>");
                return;
            }

            var state = ChildState.Get(child);
            state.DaysInStage = Math.Max(0, value);
            Log($"'{child.Name}' daysInStage = {state.DaysInStage}。");
        }

        private static void SetAway(string[] args)
        {
            if (!TryGetChild(out var child)) return;

            if (args.Length < 1 || !bool.TryParse(args[0], out var away))
            {
                Log("用法: lwwc_away <true|false>");
                return;
            }

            var state = ChildState.Get(child);

            if (away)
            {
                if (state.LessonLocation.Length == 0)
                {
                    Log("今天没课，先 lwwc_stage 4-5 并等一天，或直接看 lwwc_info。");
                    return;
                }

                CommuteService.ForceToSeat(child, state);
                Log("已把孩子送到座位。");
            }
            else
            {
                CommuteService.ForceReturnHome(child, "调试指令");
                Log("已强制回家。");
            }
        }

        private static void PlayEvent(string[] args)
        {
            if (!TryGetChild(out var child)) return;

            if (args.Length < 1)
            {
                Log("用法: lwwc_event <first_walk|childhood|first_school|dream|adulthood>");
                return;
            }

            StageScenes.PlayNow(child, ChildState.Get(child), args[0]);
            Log("已请求播放场景 " + args[0] + "。");
        }

        private static void PrintFlags()
        {
            if (!TryGetChild(out var child)) return;

            var state = ChildState.Get(child);
            Log($"'{child.Name}' flags = [{string.Join(",", state.Flags)}]");
            Log($"'{child.Name}' pending = [{string.Join(",", state.PendingEvents)}]");
            Log($"'{child.Name}' 已迁移 = {state.HasMigrated}");
        }

        private static void CollectKegs()
        {
            var result = KegCollector.CollectAll();

            if (result.NoChest)
            {
                Log("没有家庭箱，代收跳过。");
                return;
            }

            Log($"批量代收：搬走 {result.Moved} 件，失败 {result.Blocked} 件，扫到 {result.KegsScanned} 个桶。");
        }

        private static void RunChore()
        {
            Log(KegChore.ForceOnce());
        }

        private static void CreateChest()
        {
            var location = Game1.player.currentLocation;
            var tile = Game1.player.Tile;

            var chest = FamilyChest.CreateAt(location, tile);
            Log(chest != null ? $"已在 {location.NameOrUniqueName} {tile} 生成家庭箱。" : "生成家庭箱失败（格子被占用？）。");
        }

        private static void ForceWork()
        {
            if (!TryGetChild(out var child)) return;

            ChildState.Get(child).WorkDoneToday = false;
            AbilityService.RunDailyWork();
            Log("已结算一次孩子能力（详见日志）。");
        }

        private static void Rollback()
        {
            if (!TryGetChild(out var child)) return;

            var state = ChildState.Get(child);
            if (state.Stage <= Stage.Baby)
            {
                Log("已经在最低阶段。");
                return;
            }

            GrowthService.SetStage(child, state.Stage - 1);
            Log($"'{child.Name}' 回退到阶段 {(int)state.Stage}。");
        }

        private static void ReloadAssets()
        {
            var count = 0;

            foreach (var child in ChildRegistry.All())
            {
                Content.AssetInjector.InvalidateChildAssets(child);
                child.InvalidateMasterSchedule();
                count++;
            }

            Log($"已重建 {count} 个孩子的资产缓存。");
        }

        private static void PrintSchedule(string[] args)
        {
            if (args.Length < 1)
            {
                Log("用法: lwwc_schedule <NPC名>（例如 lwwc_schedule Penny）");
                return;
            }

            var name = args[0];
            var npc = Utility.getAllCharacters()?.FirstOrDefault(n =>
                n != null && string.Equals(n.Name, name, StringComparison.OrdinalIgnoreCase));

            if (npc == null)
            {
                Log($"找不到 NPC '{name}'。");
                return;
            }

            foreach (var line in Timetable.DescribeSchedule(npc)) Log(line);
        }

        private static void PrintClassroom()
        {
            foreach (var line in SeatService.Describe()) Log(line);
        }

        private static bool TryGetChild(out StardewValley.Characters.Child child)
        {
            var children = ChildRegistry.All();
            if (children.Count == 0)
            {
                child = null!;
                Log("当前存档里没有孩子。");
                return false;
            }

            child = children[0];
            return true;
        }
    }
}
