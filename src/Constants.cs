namespace LifeWithWifeAndChild
{
    /// <summary>全局常量：modData 键、资产键、地点名。所有魔法字符串集中在这里。</summary>
    internal static class Constants
    {
        internal const string LogPrefix = "[LWWC]";
        internal const string UniqueId = "LWWC.LifeWithWifeAndChild";

        /// <summary>家庭箱物品的未限定 id（1.6 要求带命名空间，避免与 (O) 冲突）。</summary>
        internal const string FamilyChestItemId = "LifeWithWifeAndChild.FamilyChest";

        /// <summary>家庭箱物品的限定 id。</summary>
        internal const string FamilyChestQualifiedId = "(BC)LifeWithWifeAndChild.FamilyChest";

        // ---- 孩子 modData 键（TECH-DESIGN §6.1）----
        internal const string KeyStage = "lwwc/stage";
        internal const string KeyDaysInStage = "lwwc/daysInStage";
        internal const string KeyLastGrowthDay = "lwwc/lastGrowthDay";
        internal const string KeyFlags = "lwwc/flags";
        internal const string KeyPendingEvents = "lwwc/pendingEvents";
        internal const string KeyAway = "lwwc/away";
        internal const string KeyAwayUntil = "lwwc/awayUntil";
        internal const string KeyHomeLocation = "lwwc/homeLocation";
        internal const string KeyWorkDoneToday = "lwwc/workDoneToday";
        internal const string KeyNameKey = "lwwc/nameKey";
        internal const string KeyMigrated = "lwwc/migrated";
        internal const string KeyPrevStage = "lwwc/prevStage";
        internal const string KeyScenesSeen = "lwwc/scenesSeen";

        // ---- 上课（v1.1）----
        internal const string KeyLessonLocation = "lwwc/lesson/location";
        internal const string KeyLessonStart = "lwwc/lesson/start";
        internal const string KeyLessonEnd = "lwwc/lesson/end";
        internal const string KeyLessonSeat = "lwwc/lesson/seat";
        internal const string KeyLessonDay = "lwwc/lesson/day";

        // ---- 周末收桶家务（v1.1）----
        internal const string KeyChoreDay = "lwwc/chore/day";
        internal const string KeyChoreState = "lwwc/chore/state";
        internal const string KeyChoreTarget = "lwwc/chore/target";
        internal const string KeyChoreSkipped = "lwwc/chore/skipped";
        internal const string KeyChoreCount = "lwwc/chore/count";
        internal const string KeyChoreWalkTicks = "lwwc/chore/walkTicks";

        // ---- 家庭箱 / 玩家 modData 键 ----
        internal const string KeyFamilyChest = "lwwc/familyChest";
        internal const string KeyFamilyChestOwner = "lwwc/familyChestOwner";
        internal const string KeyFamilyChestFull = "lwwc/familyChestFull";
        internal const string KeyChestGiven = "lwwc/chestGiven";

        // ---- 资产键 ----
        internal const string AssetSchedulesPrefix = "Characters/schedules/";
        internal const string AssetDialoguePrefix = "Characters/Dialogue/";
        internal const string AssetBigCraftables = "Data/BigCraftables";

        // ---- 地点 ----
        internal const string HomeLocation = "FarmHouse";
        internal const string FarmLocation = "Farm";
        internal const string SchoolLocationVanilla = "ArchaeologyHouse";
        internal const string SchoolLocationCommunity = "CommunityCenter";

        // ---- 日程键（我们自己注入的键，全部加 lwwc 前缀）----
        internal const string ScheduleLessonKey = "lwwc_lesson";

        // ---- 剧情 flag 前缀 ----
        internal const string MailPrefix = "lwwc_";

        // ---- 校历 ----
        internal const string TeacherName = "Penny";
        internal const int ClassEndCap = 1700;
    }
}
