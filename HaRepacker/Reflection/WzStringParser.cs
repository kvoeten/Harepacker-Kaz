using MapleLib.WzLib.Serializer.Model;
using MapleLib.WzLib.WzProperties;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MapleLib.WzLib.Serializer.Parsers
{
    public class WzStringParser : WzReflectionParser
    {
        public override string TargetFileName => "String.wz";
        private string _jsonBasePath;

        public override void DefineSchema(ModelBuilder modelBuilder)
        {
            // String.wz doesn't export its own data, it enriches other parsers
        }

        public override void ParseAndExportData(WzDirectory wzDir, ModelBuilder modelBuilder, string outputPath)
        {
            // Find the String.wz JSON directory
            _jsonBasePath = Path.Combine(Path.GetDirectoryName(typeof(WzStringParser).Assembly.Location), "Reflection", "String.wz");
            
            if (!Directory.Exists(_jsonBasePath))
            {
                Debug.WriteLine($"Warning: String.wz JSON directory not found at {_jsonBasePath}");
                return;
            }

            Debug.WriteLine("Parsing String.wz JSON files...");
            WzStringCache.Clear();

            // Parse item strings
            ParseItemStrings("Eqp.img.json", "item");
            ParseItemStrings("Etc.img.json", "item");
            ParseItemStrings("Consume.img.json", "item");
            ParseItemStrings("Ins.img.json", "item");
            ParseItemStrings("Cash.img.json", "item");
            ParseItemStrings("Pet.img.json", "item");

            // Parse map strings
            ParseMapStrings("Map.img.json");

            // Parse mob strings
            ParseMobStrings("Mob.img.json");

            // Parse NPC strings
            ParseNpcStrings("Npc.img.json");

            // Parse skill strings
            ParseSkillStrings("Skill.img.json");

            Debug.WriteLine("String.wz parsing complete.");
        }

        private void ParseItemStrings(string fileName, string type)
        {
            string filePath = Path.Combine(_jsonBasePath, fileName);
            if (!File.Exists(filePath))
            {
                Debug.WriteLine($"Warning: {fileName} not found");
                return;
            }

            var json = JObject.Parse(File.ReadAllText(filePath));
            
            // For Eqp.img, iterate through categories (Cap, Coat, etc.)
            // For others, items are at root level
            foreach (var category in json.Properties())
            {
                if (category.Name.StartsWith("_")) continue; // Skip metadata

                var categoryObj = category.Value as JObject;
                if (categoryObj == null) continue;

                // Check if this is a category container (for Eqp) or direct items
                foreach (var item in categoryObj.Properties())
                {
                    if (item.Name.StartsWith("_")) continue;

                    // Try to parse as item ID
                    if (int.TryParse(item.Name, out int itemId))
                    {
                        ExtractItemStrings(item.Value as JObject, type, itemId);
                    }
                    else
                    {
                        // This might be a subcategory, iterate its children
                        var subObj = item.Value as JObject;
                        if (subObj != null)
                        {
                            foreach (var subItem in subObj.Properties())
                            {
                                if (subItem.Name.StartsWith("_")) continue;
                                if (int.TryParse(subItem.Name, out int subItemId))
                                {
                                    ExtractItemStrings(subItem.Value as JObject, type, subItemId);
                                }
                            }
                        }
                    }
                }
            }
        }

        private void ExtractItemStrings(JObject itemObj, string type, int id)
        {
            if (itemObj == null) return;

            var name = itemObj["name"]?["_value"]?.ToString();
            var desc = itemObj["desc"]?["_value"]?.ToString();

            if (name != null) WzStringCache.Set(type, id, "name", name);
            if (desc != null) WzStringCache.Set(type, id, "desc", desc);
        }

        private void ParseMapStrings(string fileName)
        {
            string filePath = Path.Combine(_jsonBasePath, fileName);
            if (!File.Exists(filePath))
            {
                Debug.WriteLine($"Warning: {fileName} not found");
                return;
            }

            var json = JObject.Parse(File.ReadAllText(filePath));
            var maple = json["maple"] as JObject;
            if (maple == null) return;

            foreach (var map in maple.Properties())
            {
                if (map.Name.StartsWith("_")) continue;
                if (!int.TryParse(map.Name, out int mapId)) continue;

                var mapObj = map.Value as JObject;
                if (mapObj == null) continue;

                var streetName = mapObj["streetName"]?["_value"]?.ToString();
                var mapName = mapObj["mapName"]?["_value"]?.ToString();
                var mapDesc = mapObj["mapDesc"]?["_value"]?.ToString();

                if (streetName != null) WzStringCache.Set("map", mapId, "streetName", streetName);
                if (mapName != null) WzStringCache.Set("map", mapId, "mapName", mapName);
                if (mapDesc != null) WzStringCache.Set("map", mapId, "mapDesc", mapDesc);
            }
        }

        private void ParseMobStrings(string fileName)
        {
            string filePath = Path.Combine(_jsonBasePath, fileName);
            if (!File.Exists(filePath))
            {
                Debug.WriteLine($"Warning: {fileName} not found");
                return;
            }

            var json = JObject.Parse(File.ReadAllText(filePath));

            foreach (var mob in json.Properties())
            {
                if (mob.Name.StartsWith("_")) continue;
                if (!int.TryParse(mob.Name, out int mobId)) continue;

                var mobObj = mob.Value as JObject;
                if (mobObj == null) continue;

                var name = mobObj["name"]?["_value"]?.ToString();
                if (name != null) WzStringCache.Set("mob", mobId, "name", name);
            }
        }

        private void ParseNpcStrings(string fileName)
        {
            string filePath = Path.Combine(_jsonBasePath, fileName);
            if (!File.Exists(filePath))
            {
                Debug.WriteLine($"Warning: {fileName} not found");
                return;
            }

            var json = JObject.Parse(File.ReadAllText(filePath));

            foreach (var npc in json.Properties())
            {
                if (npc.Name.StartsWith("_")) continue;
                if (!int.TryParse(npc.Name, out int npcId)) continue;

                var npcObj = npc.Value as JObject;
                if (npcObj == null) continue;

                var name = npcObj["name"]?["_value"]?.ToString();
                var desc = npcObj["desc"]?["_value"]?.ToString();

                if (name != null) WzStringCache.Set("npc", npcId, "name", name);
                if (desc != null) WzStringCache.Set("npc", npcId, "desc", desc);
            }
        }

        private void ParseSkillStrings(string fileName)
        {
            string filePath = Path.Combine(_jsonBasePath, fileName);
            if (!File.Exists(filePath))
            {
                Debug.WriteLine($"Warning: {fileName} not found");
                return;
            }

            var json = JObject.Parse(File.ReadAllText(filePath));

            foreach (var skill in json.Properties())
            {
                if (skill.Name.StartsWith("_")) continue;
                if (!int.TryParse(skill.Name, out int skillId)) continue;

                var skillObj = skill.Value as JObject;
                if (skillObj == null) continue;

                var name = skillObj["name"]?["_value"]?.ToString();
                var desc = skillObj["desc"]?["_value"]?.ToString();

                if (name != null) WzStringCache.Set("skill", skillId, "name", name);
                if (desc != null) WzStringCache.Set("skill", skillId, "desc", desc);
            }
        }
    }
}
