using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Mods;

namespace LifeWithWifeAndChild.Core
{
    /// <summary>
    /// 唯一的状态入口（TECH-DESIGN §5.3）：孩子的阶段只从 <c>lwwc/stage</c> 读，
    /// 内存里的 <see cref="StardewValley.NPC.Age"/> 只是派生值，任何地方都不允许反推阶段。
    /// </summary>
    internal sealed class ChildState
    {
        private readonly Child child;

        private ChildState(Child child)
        {
            this.child = child;
        }

        internal Child Child => child;

        internal static ChildState Get(Child child)
        {
            return new ChildState(child);
        }

        private ModDataDictionary Data => child.modData;

        /// <summary>这个孩子是否已被本 mod 接管（有 lwwc/stage 键）。</summary>
        internal bool IsManaged => Data.Has(Constants.KeyStage);

        internal Stage Stage
        {
            get => StageExtensions.Clamp(Data.GetInt(Constants.KeyStage));
            set
            {
                var clamped = StageExtensions.Clamp((int)value);
                if (Stage == clamped) return;
                Data.SetInt(Constants.KeyPrevStage, (int)Stage);
                Data.SetInt(Constants.KeyStage, (int)clamped);
            }
        }

        /// <summary>直接写阶段，不做"同值跳过"判断（迁移/调试用）。</summary>
        internal void ForceStage(Stage value)
        {
            Data.SetInt(Constants.KeyStage, (int)StageExtensions.Clamp((int)value));
        }

        internal Stage PreviousStage => StageExtensions.Clamp(Data.GetInt(Constants.KeyPrevStage));

        internal int DaysInStage
        {
            get => Math.Max(0, Data.GetInt(Constants.KeyDaysInStage));
            set => Data.SetInt(Constants.KeyDaysInStage, Math.Max(0, value));
        }

        internal int LastGrowthDay
        {
            get => Data.GetInt(Constants.KeyLastGrowthDay, -1);
            set => Data.SetInt(Constants.KeyLastGrowthDay, value);
        }

        internal bool Away
        {
            get => Data.GetBool(Constants.KeyAway);
            set => Data.SetBool(Constants.KeyAway, value);
        }

        internal int AwayUntil
        {
            get => Data.GetInt(Constants.KeyAwayUntil, -1);
            set => Data.SetInt(Constants.KeyAwayUntil, value);
        }

        internal bool WorkDoneToday
        {
            get => Data.GetBool(Constants.KeyWorkDoneToday);
            set => Data.SetBool(Constants.KeyWorkDoneToday, value);
        }

        internal string NameKey
        {
            get => Data.GetString(Constants.KeyNameKey);
            set => Data[Constants.KeyNameKey] = value ?? string.Empty;
        }

        // ---- 上课（v1.1）----

        /// <summary>今天上课的地图；空字符串表示今天没课。</summary>
        internal string LessonLocation
        {
            get => Data.GetString(Constants.KeyLessonLocation);
            set => Data.Write(Constants.KeyLessonLocation, value ?? string.Empty);
        }

        internal int LessonStart
        {
            get => Data.GetInt(Constants.KeyLessonStart);
            set => Data.SetInt(Constants.KeyLessonStart, value);
        }

        internal int LessonEnd
        {
            get => Data.GetInt(Constants.KeyLessonEnd);
            set => Data.SetInt(Constants.KeyLessonEnd, value);
        }

        /// <summary>座位格；Zero 表示未知。</summary>
        internal Microsoft.Xna.Framework.Vector2 LessonSeat
        {
            get => ModDataVector.Read(Data, Constants.KeyLessonSeat);
            set => ModDataVector.Write(Data, Constants.KeyLessonSeat, value);
        }

        internal int LessonDay
        {
            get => Data.GetInt(Constants.KeyLessonDay, -1);
            set => Data.SetInt(Constants.KeyLessonDay, value);
        }

        // ---- 周末收桶家务（v1.1）----

        internal int ChoreDay
        {
            get => Data.GetInt(Constants.KeyChoreDay, -1);
            set => Data.SetInt(Constants.KeyChoreDay, value);
        }

        internal string ChoreState
        {
            get => Data.GetString(Constants.KeyChoreState);
            set => Data.Write(Constants.KeyChoreState, value ?? string.Empty);
        }

        internal Microsoft.Xna.Framework.Vector2 ChoreTarget
        {
            get => ModDataVector.Read(Data, Constants.KeyChoreTarget);
            set => ModDataVector.Write(Data, Constants.KeyChoreTarget, value);
        }

        internal List<string> ChoreSkipped => Data.GetList(Constants.KeyChoreSkipped);

        internal void SetChoreSkipped(IEnumerable<string> tiles)
        {
            Data.SetList(Constants.KeyChoreSkipped, tiles);
        }

        internal int ChoreCount
        {
            get => Data.GetInt(Constants.KeyChoreCount);
            set => Data.SetInt(Constants.KeyChoreCount, value);
        }

        internal int ChoreWalkTicks
        {
            get => Data.GetInt(Constants.KeyChoreWalkTicks);
            set => Data.SetInt(Constants.KeyChoreWalkTicks, value);
        }

        internal List<string> Flags => Data.GetList(Constants.KeyFlags);

        internal bool HasFlag(string flag)
        {
            return Flags.Contains(flag);
        }

        internal void AddFlag(string flag)
        {
            if (string.IsNullOrWhiteSpace(flag)) return;
            var flags = Flags;
            if (flags.Contains(flag)) return;
            flags.Add(flag);
            Data.SetList(Constants.KeyFlags, flags);
        }

        internal List<string> PendingEvents => Data.GetList(Constants.KeyPendingEvents);

        internal void QueueEvent(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return;
            var pending = PendingEvents;
            if (pending.Contains(id)) return;
            pending.Add(id);
            Data.SetList(Constants.KeyPendingEvents, pending);
        }

        internal void RemoveEvent(string id)
        {
            var pending = PendingEvents;
            if (!pending.Remove(id)) return;
            Data.SetList(Constants.KeyPendingEvents, pending);
        }

        internal bool HasMigrated => Data.GetBool(Constants.KeyMigrated);

        internal void MarkMigrated()
        {
            Data.SetBool(Constants.KeyMigrated, true);
        }

        /// <summary>把状态刷进原版 Age（派生值），让外观/行为与该阶段一致。</summary>
        internal void ApplyVanillaAge()
        {
            try
            {
                var age = Stage.VanillaAge();
                if (child.Age != age)
                    child.Age = age;

                child.speed = Stage >= Stage.Child ? 4 : 1;
            }
            catch (Exception ex)
            {
                ModLog.Error("ApplyVanillaAge failed for child '" + child?.Name + "'.", ex);
            }
        }

        public override string ToString()
        {
            return string.Format(
                "stage={0} daysInStage={1} lastGrowthDay={2} away={3} flags=[{4}] pending=[{5}]",
                (int)Stage, DaysInStage, LastGrowthDay, Away,
                string.Join("|", Flags), string.Join("|", PendingEvents));
        }
    }

    /// <summary>把 Vector2 存进 modData（"x,y"）。</summary>
    internal static class ModDataVector
    {
        internal static Microsoft.Xna.Framework.Vector2 Read(ModDataDictionary data, string key)
        {
            var raw = data.GetString(key);
            if (string.IsNullOrWhiteSpace(raw)) return Microsoft.Xna.Framework.Vector2.Zero;

            var parts = raw.Split(',');
            if (parts.Length != 2) return Microsoft.Xna.Framework.Vector2.Zero;

            if (int.TryParse(parts[0], out var x) && int.TryParse(parts[1], out var y))
                return new Microsoft.Xna.Framework.Vector2(x, y);

            return Microsoft.Xna.Framework.Vector2.Zero;
        }

        internal static void Write(ModDataDictionary data, string key, Microsoft.Xna.Framework.Vector2 value)
        {
            if (value == Microsoft.Xna.Framework.Vector2.Zero)
            {
                data.Write(key, string.Empty);
                return;
            }

            data.Write(key, (int)value.X + "," + (int)value.Y);
        }
    }
}
