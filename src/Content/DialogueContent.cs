using System;
using System.Collections.Generic;
using StardewValley;
using StardewValley.Characters;
using LifeWithWifeAndChild.Core;

namespace LifeWithWifeAndChild.Content
{
    /// <summary>
    /// 孩子的对话资产（<c>Characters/Dialogue/&lt;孩子名&gt;</c>）。
    ///
    /// 原版对孩子的右键是被 <c>Child.checkAction</c> 直接拦掉的（只做 friendship + emote），
    /// 所以真正把台词显示出来的是 <c>ChildTalkPatch</c>；这里注入资产是为了两件事：
    ///   1. 让台词走"数据资产"这条路（Content Patcher / 其它 mod 可以继续改）；
    ///   2. 给外部工具一个可以查看的键表。
    /// 内容从 i18n 生成，因此中英文切换不需要改代码。
    /// </summary>
    internal static class DialogueContent
    {
        internal static Dictionary<string, string> Build(Child child, ChildState state)
        {
            var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                for (var stage = 0; stage <= StageExtensions.MaxStage; stage++)
                {
                    var key = "dialogue.stage" + stage;
                    if (I18nExtensions.Exists(key))
                        data["lwwc/" + key] = I18n.Get(key, new { child = child.Name });
                }

                foreach (var variant in new[] { "dream", "rebel" })
                {
                    for (var stage = 5; stage <= StageExtensions.MaxStage; stage++)
                    {
                        var key = "dialogue.stage" + stage + "." + variant;
                        if (I18nExtensions.Exists(key))
                            data["lwwc/" + key] = I18n.Get(key, new { child = child.Name });
                    }
                }

                if (I18nExtensions.Exists("dialogue.fallback"))
                    data["lwwc/dialogue.fallback"] = I18n.Get("dialogue.fallback", new { child = child.Name });

                // 原版会去找 <Name>_Entry 之类的键，这里留一个空的"在家"台词，避免出现奇怪的回退
                data["lwwc/entry"] = string.Empty;
            }
            catch (Exception ex)
            {
                ModLog.Error("DialogueContent.Build failed.", ex);
            }

            return data;
        }

        /// <summary>从资产里取一条台词（资产被其它 mod 改过时以资产为准）。</summary>
        internal static string? TryGetLine(Child child, string key)
        {
            try
            {
                var assetName = NameKey.DialogueAssetName(child);
                var data = ModEntry.Helper.GameContent.Load<Dictionary<string, string>>(assetName);
                if (data != null && data.TryGetValue("lwwc/" + key, out var line) && !string.IsNullOrWhiteSpace(line))
                    return line;
            }
            catch
            {
                // 资产读不到就走 i18n 回退，不报错
            }

            return null;
        }
    }
}
