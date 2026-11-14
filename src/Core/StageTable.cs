using System;

namespace LifeWithWifeAndChild.Core
{
    /// <summary>
    /// 阶段 → 原版投影天数 / 能力 / 事件 的配置表（TECH-DESIGN §6.3）。
    /// 数值不写在业务代码里，全部集中在这张表 + ModConfig。
    /// </summary>
    internal static class StageTable
    {
        /// <summary>
        /// 把本 mod 的阶段投影成"原版 daysOld 等价天数"。
        /// 写进 daysOld 后原版还会 +1，所以实际比较值是返回值 + 1：
        /// 0→1（&lt;13 Age0）、13→14（13..26 Age1）、27→28（27..54 Age2）、55→56（&gt;=55 Age3）。
        /// </summary>
        internal static int SyntheticDaysOld(Stage stage)
        {
            switch (stage)
            {
                case Stage.Baby: return 0;
                case Stage.Crawler: return 13;
                case Stage.Toddler: return 27;
                default: return 55; // 阶段 3 之后统一沿用儿童外观
            }
        }

        /// <summary>老存档折算：用原版真实阈值 13 / 27 / 55 反推（TECH-DESIGN §1.2）。</summary>
        internal static Stage FromVanillaDaysOld(int daysOld)
        {
            if (daysOld >= 55) return Stage.Child;
            if (daysOld >= 27) return Stage.Toddler;
            if (daysOld >= 13) return Stage.Crawler;
            return Stage.Baby;
        }

        /// <summary>该阶段解锁的能力 id（为空表示还没有能力）。</summary>
        internal static string? PrimaryAbility(Stage stage)
        {
            switch (stage)
            {
                case Stage.Child: return "water_crops";
                case Stage.School: return "collect_produce";
                case Stage.Teen: return "harvest_crops";
                case Stage.Adult: return "sell_produce";
                default: return null;
            }
        }

        /// <summary>该阶段的能力清单（成年后叠加：既能收作物也能代售）。</summary>
        internal static string[] Abilities(Stage stage)
        {
            if (stage >= Stage.Adult) return new[] { "harvest_crops", "sell_produce", "collect_produce" };
            if (stage == Stage.Teen) return new[] { "harvest_crops", "collect_produce" };
            if (stage == Stage.School) return new[] { "collect_produce" };
            if (stage == Stage.Child) return new[] { "water_crops" };
            return Array.Empty<string>();
        }

        /// <summary>阶段推进时要播的剧情场景 id（DESIGN.md §3 的"一个事件"）。</summary>
        internal static string? StageSceneId(Stage stage)
        {
            switch (stage)
            {
                case Stage.Crawler: return "first_walk";
                case Stage.Child: return "childhood";
                case Stage.School: return "first_school";
                case Stage.Teen: return "dream";
                case Stage.Adult: return "adulthood";
                default: return null;
            }
        }

        /// <summary>阶段推进时写的 mail flag，供存档/其它系统判断。</summary>
        internal static string StageMailFlag(Stage stage)
        {
            return Constants.MailPrefix + "stage_" + (int)stage;
        }
    }
}
