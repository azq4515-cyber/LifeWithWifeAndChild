using System;
using StardewModdingAPI;
using StardewValley.GameData.BigCraftables;
using LifeWithWifeAndChild.Core;

namespace LifeWithWifeAndChild.Content
{
    /// <summary>
    /// 家庭箱物品定义（<c>Data/BigCraftables</c> 追加一条）。
    ///
    /// 外观上刻意**复用原版箱子的贴图**：从已有条目 "130"（原版箱子）复制 Texture / SpriteIndex。
    /// 这样零美术资源、看起来就是个箱子，而且不会因为贴图索引算错变成一张空白图。
    /// </summary>
    internal static class BigCraftableContent
    {
        private const string VanillaChestId = "130";

        internal static void Apply(IAssetData asset)
        {
            try
            {
                var data = asset.AsDictionary<string, BigCraftableData>().Data;

                var entry = new BigCraftableData
                {
                    Name = "Family Chest",
                    DisplayName = I18n.Get("item.family_chest.name"),
                    Description = I18n.Get("item.family_chest.description"),
                    Price = 0,
                    Fragility = 0,
                    CanBePlacedIndoors = true,
                    CanBePlacedOutdoors = true,
                    IsLamp = false,
                    ContextTags = new System.Collections.Generic.List<string> { "lwwc_family_chest" },
                    CustomFields = new System.Collections.Generic.Dictionary<string, string>
                    {
                        { "LWWCFamilyChest", "true" },
                    },
                };

                if (data.TryGetValue(VanillaChestId, out var vanilla))
                {
                    entry.Texture = vanilla.Texture;
                    entry.SpriteIndex = vanilla.SpriteIndex;
                }

                data[Constants.FamilyChestItemId] = entry;
            }
            catch (Exception ex)
            {
                ModLog.Error("BigCraftableContent.Apply failed.", ex);
            }
        }
    }
}
