namespace LifeWithWifeAndChild.Core
{
    /// <summary>年龄阶梯（DESIGN.md §3）：原版 0–3，新增 4 学龄 / 5 少年 / 6 成年。</summary>
    internal enum Stage
    {
        Baby = 0,
        Crawler = 1,
        Toddler = 2,
        Child = 3,
        School = 4,
        Teen = 5,
        Adult = 6,
    }

    internal static class StageExtensions
    {
        internal const int MaxStage = 6;

        internal static Stage Clamp(int value)
        {
            if (value < 0) return Stage.Baby;
            if (value > MaxStage) return Stage.Adult;
            return (Stage)value;
        }

        /// <summary>i18n 里阶段名的键，例如 <c>stage.4.name</c>。</summary>
        internal static string NameKey(this Stage stage)
        {
            return "stage." + (int)stage + ".name";
        }

        /// <summary>
        /// 上学的阶段。按 DESIGN.md §3：阶段 4 学龄「农舍 → 学堂」、阶段 5 少年「镇上 + 农场」，
        /// 阶段 6 成年是「农场为主」，不再天天去学堂。
        /// </summary>
        internal static bool GoesToSchool(this Stage stage)
        {
            return stage == Stage.School || stage == Stage.Teen;
        }

        /// <summary>阶段 3 之后沿用儿童外观，所以原版 Age 一律投影到 3。</summary>
        internal static int VanillaAge(this Stage stage)
        {
            return stage >= Stage.Child ? 3 : (int)stage;
        }
    }
}
