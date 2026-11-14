namespace LifeWithWifeAndChild
{
    /// <summary>配置项（TECH-DESIGN §6.2 + v1.1 §16.6）。</summary>
    public sealed class ModConfig
    {
        /// <summary>每级需要的天数，默认 7（DESIGN.md §3.1 全阶段统一节奏）。</summary>
        public int GrowthSpeed { get; set; } = 7;

        /// <summary>孩子能力总开关（浇水/收蛋/收作物/代售）。</summary>
        public bool EnableChildWork { get; set; } = true;

        /// <summary>孩子干活的时刻（游戏内小时）。</summary>
        public int ChildWorkHour { get; set; } = 12;

        /// <summary>是否注册调试指令。</summary>
        public bool EnableDebugCommands { get; set; } = true;

        /// <summary>详细日志（每日结算、状态写入）。</summary>
        public bool VerboseLogging { get; set; } = false;

        // ---- 上学（v1.1）----

        /// <summary>是否上课。关闭后孩子一律留家。</summary>
        public bool EnableLesson { get; set; } = true;

        /// <summary>上课期间是否把孩子锁在座位上并面朝老师。</summary>
        public bool EnableSeat { get; set; } = true;


        // ---- 周末收桶（v1.1）----

        /// <summary>周六日孩子是否替家里收成熟的酿酒桶。</summary>
        public bool EnableWeekendKegChore { get; set; } = true;

        /// <summary>收桶开工的游戏内小时。</summary>
        public int KegChoreStartHour { get; set; } = 9;

        /// <summary>每天最多收几个桶。</summary>
        public int MaxKegsPerDay { get; set; } = 40;

        /// <summary>预留：儿童坐姿图的资产名（如 "Characters/LWWC/SeatedChild"）。留空则用原精灵。</summary>
        public string SeatedSprite { get; set; } = string.Empty;

        // ---- 能力上限 ----

        /// <summary>每天最多替玩家浇几格地。</summary>
        public int MaxWaterTiles { get; set; } = 8;

        /// <summary>每天最多替玩家收几株成熟作物。</summary>
        public int MaxHarvestCrops { get; set; } = 10;

        /// <summary>每天最多替玩家代售几件物品（从出货箱取）。</summary>
        public int MaxSellItems { get; set; } = 10;

        /// <summary>代售折价系数（0.5–1.0）。</summary>
        public float SellPriceMultiplier { get; set; } = 0.9f;

        /// <summary>把越界的值拉回安全范围，避免坏配置把游戏搞坏。</summary>
        internal void Sanitize()
        {
            if (GrowthSpeed < 1) GrowthSpeed = 1;
            if (GrowthSpeed > 60) GrowthSpeed = 60;

            if (ChildWorkHour < 6) ChildWorkHour = 6;
            if (ChildWorkHour > 22) ChildWorkHour = 22;

            if (KegChoreStartHour < 6) KegChoreStartHour = 6;
            if (KegChoreStartHour > 22) KegChoreStartHour = 22;

            if (MaxKegsPerDay < 0) MaxKegsPerDay = 0;
            if (MaxKegsPerDay > 200) MaxKegsPerDay = 200;

            if (MaxWaterTiles < 0) MaxWaterTiles = 0;
            if (MaxWaterTiles > 100) MaxWaterTiles = 100;

            if (MaxHarvestCrops < 0) MaxHarvestCrops = 0;
            if (MaxHarvestCrops > 200) MaxHarvestCrops = 200;

            if (MaxSellItems < 0) MaxSellItems = 0;
            if (MaxSellItems > 100) MaxSellItems = 100;

            if (SellPriceMultiplier < 0.05f) SellPriceMultiplier = 0.05f;
            if (SellPriceMultiplier > 1f) SellPriceMultiplier = 1f;

            SeatedSprite ??= string.Empty;
        }
    }
}
