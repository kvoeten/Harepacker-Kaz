using System.Collections.Generic;

namespace MapleLib.WzLib.Serializer
{
    /// <summary>
    /// Static cache for storing string data from String.wz
    /// </summary>
    public static class WzStringCache
    {
        private static Dictionary<(string type, int id, string field), string> _cache = new Dictionary<(string type, int id, string field), string>();

        public static void Set(string type, int id, string field, string value)
        {
            _cache[(type, id, field)] = value;
        }

        public static string Get(string type, int id, string field)
        {
            return _cache.TryGetValue((type, id, field), out var value) ? value : null;
        }

        public static void Clear()
        {
            _cache.Clear();
        }
    }
}
