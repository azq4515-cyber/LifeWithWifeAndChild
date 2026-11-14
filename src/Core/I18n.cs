using StardewModdingAPI;

namespace LifeWithWifeAndChild.Core
{
    /// <summary>i18n 读取封装：缺键时返回键名而不是抛异常。</summary>
    internal static class I18n
    {
        internal static string Get(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return string.Empty;

            try
            {
                return ModEntry.Helper.Translation.Get(key).ToString();
            }
            catch (System.Exception ex)
            {
                ModLog.Error("i18n missing key '" + key + "'.", ex);
                return key;
            }
        }

        internal static string Get(string key, object tokens)
        {
            if (string.IsNullOrWhiteSpace(key)) return string.Empty;

            try
            {
                return ModEntry.Helper.Translation.Get(key, tokens).ToString();
            }
            catch (System.Exception ex)
            {
                ModLog.Error("i18n missing key '" + key + "'.", ex);
                return key;
            }
        }
    }
}
