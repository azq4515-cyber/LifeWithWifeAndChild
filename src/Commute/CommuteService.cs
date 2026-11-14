using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Locations;
using LifeWithWifeAndChild.Core;

namespace LifeWithWifeAndChild.Commute
{
    /// <summary>
    /// 场景归属策略（TECH-DESIGN §7.2 的接口抽象）。
    /// v1 只有方案 B（单实体）；方案 A（代理 NPC）留作降级预案，只要再实现一个类即可切换。
    /// </summary>
    internal interface IChildPresenceStrategy
    {
        void SendTo(Child child, string locationName, Vector2 tile);

        void ReturnHome(Child child);

        bool IsAway(Child child);
    }

    /// <summary>方案 B：孩子本人换图，全程只有一个实体。</summary>
    internal sealed class SingleEntityPresence : IChildPresenceStrategy
    {
        public void SendTo(Child child, string locationName, Vector2 tile)
        {
            child.controller = null;
            child.temporaryController = null;
            Game1.warpCharacter(child, locationName, tile);
        }

        public void ReturnHome(Child child)
        {
            var home = ChildRegistry.HomeOf(child);
            if (home == null) return;

            var tile = SchoolLocator.HomeSpot(child);

            child.controller = null;
            child.temporaryController = null;
            child.Halt();

            Game1.warpCharacter(child, home, tile);
        }

        public bool IsAway(Child child)
        {
            return !ChildRegistry.IsAtHome(child);
        }
    }

    /// <summary>
    /// 出门 / 回家（DESIGN.md §5.3 方案 B，TECH-DESIGN §5.2 的一天时序）。
    ///
    /// v1.1 起"今天有没有课"由 <see cref="Timetable"/> 决定：春秋的二/三/五、非雨非节日、
    /// 且 Penny 当天的日程里确实有课堂行程，才排课。其余日子孩子留家。
    /// </summary>
    internal static class CommuteService
    {
        private static readonly IChildPresenceStrategy Strategy = new SingleEntityPresence();
        private static readonly HashSet<string> WarnedReasons = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private const int MissedDepartMinute = 30;   // 上课后 30 分钟还没到 → 强制送达
        private const int ReturnGraceMinutes = 90;   // 下课后 90 分钟还没到家 → 强制送回
        private const int ForceHomeBeforeBedMinute = 1900;

        internal static bool IsAway(Child child)
        {
            return Strategy.IsAway(child);
        }

        // ---- DayStarted ----
        internal static void OnDayStarted(Child child, ChildState state)
        {
            if (!Context.IsMainPlayer) return;

            try
            {
                state.Away = false;
                state.LessonLocation = string.Empty;
                state.LessonStart = 0;
                state.LessonEnd = 0;
                state.LessonSeat = Vector2.Zero;
                state.LessonDay = Game1.Date.TotalDays;

                ClearSchedule(child);

                if (!Timetable.IsClassDay(child, out var reason))
                {
                    if (reason != "lesson.no_class_today" && reason != "lesson.off_season")
                        WarnOnce(child, reason);

                    if (!ChildRegistry.IsAtHome(child))
                        ForceReturnHome(child, "今天不上课，归位");
                    return;
                }

                var session = Timetable.Probe(Timetable.FindTeacher());
                if (session == null)
                {
                    // 有课日但她的日程里没有课堂行程：不硬塞，孩子留家
                    ModLog.Info($"lesson: 今天是上课日，但 {Constants.TeacherName} 的日程里没有课堂行程，孩子留家。");
                    if (!ChildRegistry.IsAtHome(child))
                        ForceReturnHome(child, "今天没课，归位");
                    return;
                }

                var lesson = session.Value;
                var location = Game1.getLocationFromName(lesson.Location);
                if (location == null)
                {
                    ModLog.Warn("lesson: 找不到课堂地图 " + lesson.Location + "，孩子留家。");
                    return;
                }

                // 座位：DayStarted 时她还不在教室，先用入口当锚点；上课时 SeatService 会按她的实际位置校正
                var anchor = EntryTile(location);
                var seat = ClassroomSeat.Find(location, anchor);
                if (!seat.HasValue)
                {
                    ModLog.Warn("lesson: 课堂里找不到可坐的格子，孩子留家。");
                    return;
                }

                var script = ScheduleBuilder.BuildLessonScript(child, lesson, seat.Value);
                if (script == null) return;

                state.LessonLocation = lesson.Location;
                state.LessonStart = lesson.StartTime;
                state.LessonEnd = lesson.EndTime;
                state.LessonSeat = seat.Value;
                state.AwayUntil = lesson.EndTime;

                ScheduleBuilder.TryLoadLesson(child, script);

                ModLog.Info($"lesson: '{child.Name}' 今天上课 {lesson}，座位 {seat.Value}。");
            }
            catch (Exception ex)
            {
                ModLog.Error("CommuteService.OnDayStarted failed.", ex);
            }
        }

        private static Vector2 EntryTile(GameLocation location)
        {
            try
            {
                if (location.warps != null && location.warps.Count > 0)
                    return new Vector2(location.warps[0].X, location.warps[0].Y);

                return new Vector2(location.map.Layers[0].LayerWidth / 2, location.map.Layers[0].LayerHeight / 2);
            }
            catch
            {
                return Vector2.Zero;
            }
        }

        private static void WarnOnce(Child child, string reasonKey)
        {
            var key = NameKey.Resolve(child) + "|" + reasonKey;
            if (!WarnedReasons.Add(key)) return;

            ModLog.Warn($"school disabled for '{child.Name}': {reasonKey}");
        }

        // ---- 每 10 分钟 ----
        internal static void OnTimeChanged(int timeOfDay)
        {
            if (!Context.IsMainPlayer) return;

            try
            {
                foreach (var child in ChildRegistry.All())
                {
                    var state = ChildState.Get(child);
                    var atHome = ChildRegistry.IsAtHome(child);
                    state.Away = !atHome;

                    if (state.LessonLocation.Length == 0)
                    {
                        if (!atHome && timeOfDay >= ForceHomeBeforeBedMinute)
                            ForceReturnHome(child, "非上课日在外");
                        continue;
                    }

                    if (child.currentLocation?.NameOrUniqueName != state.LessonLocation && !atHome)
                        state.Away = true;

                    // 该上课了但还在家 → 强制送达
                    if (atHome
                        && timeOfDay >= state.LessonStart + MissedDepartMinute
                        && timeOfDay < state.LessonEnd)
                    {
                        ForceToSeat(child, state);
                        continue;
                    }

                    // 该下课了但还在外 → 兜底送回
                    if (!atHome && timeOfDay >= state.LessonEnd + ReturnGraceMinutes)
                        ForceReturnHome(child, "放学兜底");

                    if (!atHome && timeOfDay >= ForceHomeBeforeBedMinute)
                        ForceReturnHome(child, "就寝前兜底");

                    // 已经到家：卸掉日程，恢复原版儿童行为
                    if (atHome && timeOfDay >= state.LessonEnd)
                    {
                        ClearSchedule(child);
                        state.LessonLocation = string.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                ModLog.Error("CommuteService.OnTimeChanged failed.", ex);
            }
        }

        // ---- 读档修复（DESIGN.md §8 的 B 方案风险）----
        internal static void OnSaveLoaded()
        {
            if (!Context.IsMainPlayer) return;

            try
            {
                WarnedReasons.Clear();

                foreach (var child in ChildRegistry.All())
                {
                    var state = ChildState.Get(child);
                    var locations = ChildRegistry.LocateAll(child);

                    if (locations.Count > 1)
                    {
                        ModLog.Warn($"load: '{child.Name}' 同时出现在 {locations.Count} 个地图里，正在去重。");
                        for (var i = 1; i < locations.Count; i++) locations[i].characters.Remove(child);
                    }

                    if (locations.Count == 0)
                    {
                        ModLog.Warn($"load: '{child.Name}' 不属于任何地图，塞回农舍。");
                        var home = ChildRegistry.HomeOf(child);
                        if (home != null) Strategy.SendTo(child, home.NameOrUniqueName, SchoolLocator.HomeSpot(child));
                    }

                    var atHome = ChildRegistry.IsAtHome(child);
                    state.Away = !atHome;

                    if (!atHome)
                    {
                        ModLog.Warn($"load: '{child.Name}' 读档时不在农舍，强制回家。");
                        ForceReturnHome(child, "读档修复");
                    }

                    ClearSchedule(child);
                    state.LessonLocation = string.Empty;
                }
            }
            catch (Exception ex)
            {
                ModLog.Error("CommuteService.OnSaveLoaded failed.", ex);
            }
        }

        // ---- 强制搬迁 ----
        internal static void ForceReturnHome(Child child, string reason)
        {
            if (child == null) return;

            try
            {
                var state = ChildState.Get(child);

                if (ChildRegistry.IsAtHome(child))
                {
                    state.Away = false;
                    return;
                }

                var home = ChildRegistry.HomeOf(child);
                if (home == null)
                {
                    ModLog.Warn($"commute: '{child.Name}' 找不到农舍，无法回家（{reason}）。");
                    return;
                }

                Strategy.ReturnHome(child);
                ClearSchedule(child);

                state.Away = false;
                state.LessonLocation = string.Empty;

                ModLog.Trace($"commute: '{child.Name}' 回到农舍（{reason}）。");
            }
            catch (Exception ex)
            {
                ModLog.Error("CommuteService.ForceReturnHome failed.", ex);
            }
        }

        /// <summary>把孩子送到座位（上课兜底）。</summary>
        internal static void ForceToSeat(Child child, ChildState state)
        {
            if (child == null) return;

            try
            {
                if (state.LessonLocation.Length == 0) return;

                var location = Game1.getLocationFromName(state.LessonLocation);
                if (location == null) return;

                var seat = state.LessonSeat;
                if (seat == Vector2.Zero) return;

                Strategy.SendTo(child, state.LessonLocation, seat);

                state.Away = true;
                ModLog.Trace($"commute: '{child.Name}' 送到座位 {seat}（兜底）。");
            }
            catch (Exception ex)
            {
                ModLog.Error("CommuteService.ForceToSeat failed.", ex);
            }
        }

        internal static void ClearSchedule(Child child)
        {
            try
            {
                child.ClearSchedule();
                child.controller = null;
                child.temporaryController = null;
            }
            catch (Exception ex)
            {
                ModLog.Error("CommuteService.ClearSchedule failed.", ex);
            }
        }

        /// <summary>阶段变更后立刻刷新"今天该怎么安排"，让调试指令 <c>lwwc_stage</c> 立即生效。</summary>
        internal static void RefreshToday(Child child)
        {
            try
            {
                var state = ChildState.Get(child);

                if (!state.Stage.GoesToSchool())
                {
                    state.LessonLocation = string.Empty;
                    ForceReturnHome(child, "阶段不再需要上课");
                    return;
                }

                if (!Context.IsMainPlayer) return;

                OnDayStarted(child, state);

                if (Game1.timeOfDay >= state.LessonStart && Game1.timeOfDay < state.LessonEnd && state.LessonLocation.Length > 0)
                    ForceToSeat(child, state);
            }
            catch (Exception ex)
            {
                ModLog.Error("CommuteService.RefreshToday failed.", ex);
            }
        }
    }
}
