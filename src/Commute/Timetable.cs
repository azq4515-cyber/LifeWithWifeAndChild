using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using LifeWithWifeAndChild.Core;

namespace LifeWithWifeAndChild.Commute
{
    /// <summary>一堂课：在哪上、几点开始、几点结束。</summary>
    internal readonly struct ClassSession
    {
        internal readonly string Location;
        internal readonly int StartTime;
        internal readonly int EndTime;

        internal ClassSession(string location, int startTime, int endTime)
        {
            Location = location;
            StartTime = startTime;
            EndTime = endTime;
        }

        public override string ToString()
        {
            return Location + " " + StartTime + "-" + EndTime;
        }
    }

    /// <summary>
    /// 校历与课时（DESIGN.md §3.2 / §4.2）。
    ///
    /// 产品规则：**春秋两季的周二/三/五**有课，其余日子没课；雨/雷雨/绿雨停课；节日不上课。
    /// 课时不写死：读 Penny **当天已解析的日程**，她到课堂的那条是上课时间，她下一条日程是下课时间。
    ///
    /// 为什么只读：v1.0 的"往她日程里插一条"方案带来过两类故障
    /// （同一时间点重复导致解析抛异常；在 AssetRequested 里调 parseMasterSchedule 重入把她的
    /// master schedule 缓存污染成 null）。原版她本来就会去上课，注入是纯风险。
    /// </summary>
    internal static class Timetable
    {
        /// <summary>可以当作"课堂"的地点名。以后要加别的地点，改这里。</summary>
        private static readonly string[] ClassroomCandidates =
        {
            Constants.SchoolLocationVanilla,
            Constants.SchoolLocationCommunity,
        };

        /// <summary>今天该不该上学（只看规则，不看她在不在）。</summary>
        internal static bool IsClassDay(Child child, out string reason)
        {
            reason = string.Empty;

            try
            {
                if (!ModEntry.Config.EnableLesson)
                {
                    reason = "lesson.disabled";
                    return false;
                }

                var season = Game1.currentSeason;
                if (season != "spring" && season != "fall")
                {
                    reason = "lesson.off_season";
                    return false;
                }

                var day = Game1.Date.DayOfWeek;
                if (day != DayOfWeek.Tuesday && day != DayOfWeek.Wednesday && day != DayOfWeek.Friday)
                {
                    reason = "lesson.no_class_today";
                    return false;
                }

                if (Game1.isFestival() || Utility.isFestivalDay())
                {
                    reason = "lesson.festival";
                    return false;
                }

                if (Game1.isRaining || Game1.isGreenRain)
                {
                    reason = "lesson.bad_weather";
                    return false;
                }

                if (!NameKey.CanUseAssetKeys(child, out var nameReason))
                {
                    reason = nameReason;
                    return false;
                }

                reason = "lesson.ok";
                return true;
            }
            catch (Exception ex)
            {
                ModLog.Error("Timetable.IsClassDay failed.", ex);
                reason = "lesson.error";
                return false;
            }
        }

        /// <summary>找今天的老师（只找现存 NPC，不做任何写操作）。</summary>
        internal static NPC? FindTeacher()
        {
            try
            {
                return Utility.getAllCharacters()?.Find(npc =>
                    npc != null && string.Equals(npc.Name, Constants.TeacherName, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                ModLog.Error("Timetable.FindTeacher failed.", ex);
                return null;
            }
        }

        /// <summary>读她当天的日程，推出一堂课。返回 null 表示今天她不去课堂。</summary>
        internal static ClassSession? Probe(NPC? teacher)
        {
            if (teacher == null) return null;

            try
            {
                var schedule = teacher.Schedule;

                if (schedule == null || schedule.Count == 0)
                {
                    // 原版自己加载（只读，不动资产）；她今天可能本来就没日程（雨天/节日）
                    teacher.TryLoadSchedule();
                    schedule = teacher.Schedule;
                }

                if (schedule == null || schedule.Count == 0) return null;

                var startTime = int.MaxValue;
                string? location = null;

                foreach (var pair in schedule)
                {
                    var description = pair.Value;
                    if (description == null) continue;

                    var target = description.targetLocationName;
                    if (!IsClassroom(target)) continue;

                    if (pair.Key < startTime)
                    {
                        startTime = pair.Key;
                        location = target;
                    }
                }

                if (location == null || startTime == int.MaxValue) return null;

                var endTime = int.MaxValue;
                foreach (var pair in schedule)
                {
                    if (pair.Key > startTime && pair.Key < endTime) endTime = pair.Key;
                }

                if (endTime == int.MaxValue) endTime = startTime + 600;

                endTime = Math.Min(endTime, Constants.ClassEndCap);
                if (endTime <= startTime) endTime = Math.Min(Constants.ClassEndCap, startTime + 60);

                return new ClassSession(location, startTime, endTime);
            }
            catch (Exception ex)
            {
                ModLog.Error("Timetable.Probe failed.", ex);
                return null;
            }
        }

        internal static bool IsClassroom(string? locationName)
        {
            if (string.IsNullOrWhiteSpace(locationName)) return false;

            foreach (var candidate in ClassroomCandidates)
            {
                if (string.Equals(candidate, locationName, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        /// <summary>调试：把某人当天的日程摊平成人能读的行。</summary>
        internal static List<string> DescribeSchedule(NPC npc)
        {
            var lines = new List<string>();
            if (npc == null) return lines;

            try
            {
                var schedule = npc.Schedule;
                if (schedule == null || schedule.Count == 0)
                {
                    npc.TryLoadSchedule();
                    schedule = npc.Schedule;
                }

                if (schedule == null || schedule.Count == 0)
                {
                    lines.Add(npc.Name + ": 今天没有日程");
                    return lines;
                }

                lines.Add($"{npc.Name}: 日程键 = {npc.dayScheduleName.Value ?? "<null>"}");

                var times = new List<int>(schedule.Keys);
                times.Sort();

                foreach (var time in times)
                {
                    var description = schedule[time];
                    var target = description?.targetLocationName ?? "?";
                    var tile = description != null
                        ? $"({description.targetTile.X},{description.targetTile.Y})"
                        : "?";
                    var mark = IsClassroom(target) ? "  <== 课堂" : string.Empty;

                    lines.Add($"  {time} -> {target} {tile}{mark}");
                }
            }
            catch (Exception ex)
            {
                ModLog.Error("Timetable.DescribeSchedule failed.", ex);
                lines.Add(npc.Name + ": 读取日程失败");
            }

            return lines;
        }
    }
}
