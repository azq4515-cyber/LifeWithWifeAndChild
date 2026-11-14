using StardewValley;
using StardewValley.Characters;
using LifeWithWifeAndChild.Core;

namespace LifeWithWifeAndChild.Abilities
{
    /// <summary>
    /// 孩子能力接口（TECH-DESIGN §7.3）。所有能力都是"每天一次 + 有上限"，
    /// 数值从 <see cref="ModConfig"/> 与 StageTable 来，代码里不写魔法数字。
    /// </summary>
    internal interface IChildAbility
    {
        /// <summary>稳定 id，用于日志与去重。</summary>
        string Id { get; }

        /// <summary>解锁阶段。</summary>
        int MinStage { get; }

        /// <summary>今天这个能力能不能跑。</summary>
        bool CanRun(Child child, Farmer player, ChildState state);

        /// <summary>执行，返回实际做了多少（浇水格数 / 收了几件 / 卖了几件）。</summary>
        int Run(Child child, Farmer player, ChildState state);

        /// <summary>给玩家看的一句话（i18n 键 + 计数参数由实现自己拼）。</summary>
        string Describe(int count);
    }
}
