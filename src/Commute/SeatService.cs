using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Pathfinding;
using LifeWithWifeAndChild.Core;

namespace LifeWithWifeAndChild.Commute
{
    /// <summary>
    /// 课堂座位锁定（DESIGN.md §4.3）。
    ///
    /// 上课期间孩子必须待在自己的座位上、面朝讲课人、不游走。
    /// "坐着"用位置 + 朝向 + 静止来近似 —— 原版没有任何 NPC 坐姿能力
    /// （Furniture / MapSeat 的座位只认 Farmer id，Character 上没有 sit 状态）。
    /// </summary>
    internal static class SeatService
    {
        internal static void OnTimeChanged(int timeOfDay)
        {
            if (!Context.IsMainPlayer) return;
            if (!ModEntry.Config.EnableSeat) return;

            foreach (var child in ChildRegistry.All())
            {
                try
                {
                    var state = ChildState.Get(child);

                    if (!state.IsManaged) continue;
                    if (state.LessonLocation.Length == 0) continue;
                    if (state.LessonStart <= 0) continue;
                    if (timeOfDay < state.LessonStart || timeOfDay >= state.LessonEnd) continue;

                    var location = Game1.getLocationFromName(state.LessonLocation);
                    if (location == null) continue;
                    if (!ReferenceEquals(child.currentLocation, location)) continue; // 还没进教室

                    var teacher = FindTeacher(location);

                    // 她已经在教室里了：按她的实际位置校正座位（DayStarted 时只能拿入口当锚点）
                    if (teacher != null)
                    {
                        var refined = ClassroomSeat.Find(location, teacher.Tile);
                        if (refined.HasValue && refined.Value != state.LessonSeat)
                        {
                            ModLog.Trace($"seat: 座位按她的位置校正 {state.LessonSeat} -> {refined.Value}。");
                            state.LessonSeat = refined.Value;
                        }
                    }

                    Park(child, state, teacher, location);
                }
                catch (Exception ex)
                {
                    ModLog.Error("SeatService.OnTimeChanged failed for '" + child?.Name + "'.", ex);
                }
            }
        }

        private static NPC? FindTeacher(GameLocation location)
        {
            try
            {
                var teacher = location.getCharacterFromName(Constants.TeacherName);
                if (teacher != null) return teacher;
            }
            catch
            {
                // 下面再全局找一次
            }

            var global = Timetable.FindTeacher();
            return ReferenceEquals(global?.currentLocation, location) ? global : null;
        }

        private static void Park(Child child, ChildState state, NPC? teacher, GameLocation location)
        {
            var seat = state.LessonSeat;
            if (seat == Vector2.Zero) return;

            var seatPoint = new Point((int)seat.X, (int)seat.Y);

            if (child.TilePoint == seatPoint)
            {
                // 已经在座位上：停下来、面朝她、不播动画
                child.Halt();
                child.controller = null;
                child.temporaryController = null;

                try { child.Sprite?.StopAnimation(); } catch { /* 收不到就算了 */ }

                if (teacher != null)
                {
                    var direction = DirectionTo(child.Tile, teacher.Tile);
                    if (child.FacingDirection != direction) child.faceDirection(direction);
                }

                return;
            }

            // 不在座位上：走回去（已有寻路就等它跑完）
            if (child.controller != null) return;

            try
            {
                var controller = new PathFindController(child, location, seatPoint, 2);
                if (controller.pathToEndPoint == null || controller.pathToEndPoint.Count == 0)
                {
                    // 走不过去就直接落位，绝不让孩子在教室门口卡着
                    child.setTileLocation(seat);
                    ModLog.Trace($"seat: '{child.Name}' 走不到座位，直接落位 {seat}。");
                    return;
                }

                child.controller = controller;
            }
            catch (Exception ex)
            {
                ModLog.Error("SeatService.Park failed.", ex);
                try { child.setTileLocation(seat); } catch { /* ignore */ }
            }
        }

        /// <summary>0=上 1=右 2=下 3=左。</summary>
        private static int DirectionTo(Vector2 from, Vector2 to)
        {
            var dx = to.X - from.X;
            var dy = to.Y - from.Y;

            if (Math.Abs(dx) >= Math.Abs(dy))
                return dx >= 0 ? 1 : 3;

            return dy >= 0 ? 2 : 0;
        }

        /// <summary>调试：打印课堂座位情况。</summary>
        internal static List<string> Describe()
        {
            var lines = new List<string>();

            foreach (var child in ChildRegistry.All())
            {
                var state = ChildState.Get(child);

                if (state.LessonLocation.Length == 0)
                {
                    lines.Add($"'{child.Name}': 今天没课");
                    continue;
                }

                lines.Add($"'{child.Name}': 课堂 {state.LessonLocation} {state.LessonStart}-{state.LessonEnd}，座位 {state.LessonSeat}，当前位置 {ChildRegistry.DescribeLocation(child)}");
            }

            var location = Game1.getLocationFromName(Constants.SchoolLocationVanilla)
                           ?? Game1.getLocationFromName(Constants.SchoolLocationCommunity);

            if (location != null)
            {
                var teacher = FindTeacher(location);
                var anchor = teacher?.Tile ?? new Vector2(location.map.Layers[0].LayerWidth / 2, location.map.Layers[0].LayerHeight / 2);
                lines.Add($"课堂地图 {location.NameOrUniqueName}，桌子/座位：");
                lines.AddRange(ClassroomSeat.Describe(location, anchor));
            }

            return lines;
        }
    }
}
