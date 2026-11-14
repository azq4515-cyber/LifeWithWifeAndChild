using System;
using System.Globalization;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Characters;
using LifeWithWifeAndChild.Core;

namespace LifeWithWifeAndChild.Commute
{
    /// <summary>
    /// 运行时生成孩子的日程（TECH-DESIGN §7.2）。
    ///
    /// 关键实现细节（从 <c>NPC.parseMasterScheduleImpl</c> 读出来的）：
    /// 解析日程时，第一段路径的**起点**取的是 <c>DefaultMap</c> + <c>DefaultPosition</c>，
    /// 而 <c>Child</c> 的 defaultPosition 默认是 (0,0) —— 原版会直接抛
    /// "start position ... is at tile (0, 0)"。所以推送日程之前必须先把 DefaultPosition
    /// 设成孩子当前像素位置。这也是"孩子能出门"的隐形前置条件。
    /// </summary>
    internal static class ScheduleBuilder
    {
        /// <summary>
        /// 上学日程：`出门 → 座位` 与 `下课 → 农舍` 两段。
        /// 出门时间取"上课前 60 分钟"，最早 8:00。
        /// </summary>
        internal static string? BuildLessonScript(Child child, ClassSession session, Vector2 seat)
        {
            if (string.IsNullOrWhiteSpace(session.Location)) return null;
            if (seat == Vector2.Zero) return null;

            var home = ChildRegistry.HomeOf(child);
            if (home == null) return null;

            var homeSpot = SchoolLocator.HomeSpot(child);
            var depart = Math.Max(800, session.StartTime - 60);

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} {1} {2} {3} 2/{4} {5} {6} {7} 2",
                depart,
                session.Location,
                (int)seat.X,
                (int)seat.Y,
                session.EndTime,
                home.NameOrUniqueName,
                (int)homeSpot.X,
                (int)homeSpot.Y);
        }

        /// <summary>把今天的上学日程挂到孩子身上。返回是否成功。</summary>
        internal static bool TryLoadLesson(Child child, string? script)
        {
            if (child == null || string.IsNullOrWhiteSpace(script)) return false;

            try
            {
                // 解析起点：原版用 DefaultPosition，必须在此之前同步成当前位置
                child.DefaultMap = Constants.HomeLocation;
                child.DefaultPosition = child.Position;

                child.InvalidateMasterSchedule();

                var ok = child.TryLoadSchedule(Constants.ScheduleLessonKey, script);
                if (!ok)
                    ModLog.Warn($"schedule: TryLoadSchedule 失败（'{child.Name}'），今天走兜底传送。");

                return ok;
            }
            catch (Exception ex)
            {
                ModLog.Error("schedule: TryLoadLesson failed.", ex);
                return false;
            }
        }

        /// <summary>
        /// 注入 <c>Characters/schedules/&lt;孩子名&gt;</c> 资产。
        /// 这是给 Content Patcher / 其它 mod 与"原版自己的日程加载"留的入口；
        /// 我们自己的加载走 <see cref="TryLoadLesson"/>，不依赖资产缓存。
        /// </summary>
        internal static System.Collections.Generic.Dictionary<string, string> BuildScheduleAsset(Child child)
        {
            var data = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                data["lwwc_home"] = "610 " + Constants.HomeLocation + " 31 14 2";
            }
            catch (Exception ex)
            {
                ModLog.Error("schedule: BuildScheduleAsset failed.", ex);
            }

            return data;
        }
    }
}
