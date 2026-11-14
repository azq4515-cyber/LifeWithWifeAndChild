using System.Collections.Generic;
using System.Globalization;
using StardewValley.Mods;

namespace LifeWithWifeAndChild.Core
{
    /// <summary>
    /// <c>modData</c> 的读写兜底。读写全部经过这里，保证旧存档缺键 / 值损坏时不会 NRE 或抛异常。
    ///
    /// 注意：<c>ModDataDictionary</c> 继承自 Netcode 的 <c>NetStringDictionary&lt;string, NetString&gt;</c>，
    /// **不是** <c>IDictionary&lt;string,string&gt;</c>（虽然用起来很像），所以扩展方法必须写在它自己身上。
    /// </summary>
    internal static class ModDataExtensions
    {
        internal static bool Has(this ModDataDictionary data, string key)
        {
            if (data == null || string.IsNullOrWhiteSpace(key)) return false;

            try
            {
                return data.ContainsKey(key);
            }
            catch
            {
                return false;
            }
        }

        internal static string? Read(this ModDataDictionary data, string key)
        {
            if (!data.Has(key)) return null;

            try
            {
                return data[key];
            }
            catch
            {
                return null;
            }
        }

        internal static void Write(this ModDataDictionary data, string key, string value)
        {
            if (data == null || string.IsNullOrWhiteSpace(key)) return;

            try
            {
                // NetDictionary 的写会打脏标记；同值不写，避免每 10 分钟刷一次存档
                if (data.Has(key) && data[key] == value) return;

                data[key] = value;
            }
            catch (System.Exception ex)
            {
                ModLog.Error("modData write failed for '" + key + "'.", ex);
            }
        }

        internal static int GetInt(this ModDataDictionary data, string key, int fallback = 0)
        {
            var raw = data.Read(key);
            if (string.IsNullOrWhiteSpace(raw)) return fallback;

            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : fallback;
        }

        internal static void SetInt(this ModDataDictionary data, string key, int value)
        {
            data.Write(key, value.ToString(CultureInfo.InvariantCulture));
        }

        internal static bool GetBool(this ModDataDictionary data, string key, bool fallback = false)
        {
            var raw = data.Read(key);
            if (string.IsNullOrWhiteSpace(raw)) return fallback;

            return raw == "true" || raw == "1" || raw == "True";
        }

        internal static void SetBool(this ModDataDictionary data, string key, bool value)
        {
            data.Write(key, value ? "true" : "false");
        }

        internal static string GetString(this ModDataDictionary data, string key, string fallback = "")
        {
            var raw = data.Read(key);
            return string.IsNullOrWhiteSpace(raw) ? fallback : raw!;
        }

        internal static List<string> GetList(this ModDataDictionary data, string key)
        {
            var result = new List<string>();
            var raw = data.GetString(key);
            if (string.IsNullOrWhiteSpace(raw)) return result;

            foreach (var part in raw.Split(','))
            {
                var trimmed = part.Trim();
                if (trimmed.Length > 0) result.Add(trimmed);
            }

            return result;
        }

        internal static void SetList(this ModDataDictionary data, string key, IEnumerable<string> values)
        {
            data.Write(key, string.Join(",", values));
        }
    }
}
