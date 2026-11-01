using System.Collections.Generic;
using Terraria.ModLoader;
using Terraria;
using System;

namespace DZT.Common.Utils
{
    public static class BuffUtils
    {
        private static readonly Dictionary<string, int> _modBuffCache = [];

        /// <summary>
        /// 动态查找模组Buff ID，支持中英文名称匹配，并缓存结果
        /// </summary>
        public static int GetModBuffID(string chineseName, string englishName)
        {
            string cacheKey = chineseName + "|" + englishName;
            if (_modBuffCache.TryGetValue(cacheKey, out int cachedId))
                return cachedId;

            for (int i = 1; i < BuffLoader.BuffCount; i++)
            {
                string name = Lang.GetBuffName(i);
                if (string.IsNullOrEmpty(name)) continue;

                // 匹配中文或英文名称（忽略大小写）
                if (name.Contains(chineseName) ||
                    name.Contains(englishName, StringComparison.OrdinalIgnoreCase))
                {
                    _modBuffCache[cacheKey] = i;
                    return i;
                }
            }

            _modBuffCache[cacheKey] = -1; // 未找到
            return -1;
        }
    }
}