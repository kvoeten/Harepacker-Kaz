using MapleLib.WzLib.Serializer.Model;
using MapleLib.WzLib.WzProperties;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MapleLib.WzLib.Serializer.Parsers
{
    public class WzNpcParser : WzReflectionParser
    {
        public override string TargetFileName => "Npc.wz";

        // Properties to ignore during NPC parsing
        private static readonly HashSet<string> IgnoredProperties = new HashSet<string>
        {
            "adDisplay", "dcBattom", "dcBottom", "dcLeft", "dcmark", "dcMark",
            "dcMarkOnCenter", "dcRight", "dcTop", "jsonLoad", "link", "MapleTV",
            "MapleTVadX", "MapleTVadY", "MapleTVmsgX", "MapleTVmsgY", "PresentItem",
            "PvPRankingDisplay", "quarterViewYOffset", "quest", "questIconComplete",
            "questIconPerform", "questIconPreComplete", "questIconPreStart",
            "skeleton", "spine", "storebank", "talkMouseOnly"
        };

        public override void DefineSchema(ModelBuilder modelBuilder)
        {
            var npcStruct = modelBuilder.GetOrCreateStruct("Npc");
            npcStruct.AddProperty(new PropertyDef("_id", "i32", ""));
            npcStruct.AddProperty(new PropertyDef("name", "Option<String>", ""));
            npcStruct.AddProperty(new PropertyDef("desc", "Option<String>", ""));
            npcStruct.AddProperty(new PropertyDef("stand", "Option<String>", ""));
            npcStruct.AddProperty(new PropertyDef("info", "Info", ""));

            // Ensure Info struct exists
            modelBuilder.GetOrCreateStruct("Info");
        }

        public override void ParseAndExportData(WzDirectory wzDir, ModelBuilder modelBuilder, string outputPath)
        {
            var allNpcs = new List<Dictionary<string, object>>();
            var infoStruct = modelBuilder.GetOrCreateStruct("Info");

            foreach (var npcImg in wzDir.WzImages)
            {
                // Parse ID from image name (e.g., 1000.img -> 1000)
                string imgName = npcImg.Name.Replace(".img", "");
                if (!int.TryParse(imgName, out int npcId))
                {
                    // Debug.WriteLine($"Skipping image (not an ID): {npcImg.Name}");
                    continue;
                }

                var npcData = new Dictionary<string, object>();
                npcData["_id"] = npcId;

                // Add string data
                var name = WzStringCache.Get("npc", npcId, "name");
                var desc = WzStringCache.Get("npc", npcId, "desc");
                if (name != null) npcData["name"] = name;
                if (desc != null) npcData["desc"] = desc;

                // Extract stand image
                var stand = ExtractImageAsBase64(npcImg, "stand/0");
                if (stand != null) npcData["stand"] = stand;

                if (npcImg["info"] is WzSubProperty infoNode)
                {
                    // Filter out ignored properties during schema update
                    UpdateSchemaFromNodeFiltered(infoNode, infoStruct, modelBuilder, $"{npcImg.Name}/info");
                    
                    // Parse and filter data
                    npcData["info"] = ParsePropertyNodeFiltered(infoNode);
                }
                
                allNpcs.Add(npcData);
            }
            
            Debug.WriteLine($"Total npcs processed: {allNpcs.Count}");
            ExportDataToFile(allNpcs, "npcs", outputPath);
        }

        private void UpdateSchemaFromNodeFiltered(WzSubProperty propertyNode, StructDef parentStruct, ModelBuilder modelBuilder, string basePath)
        {
            if (propertyNode == null) return;

            foreach (var prop in propertyNode.WzProperties)
            {
                // Skip ignored properties
                if (IgnoredProperties.Contains(prop.Name))
                    continue;

                // Skip numeric properties as they usually represent array-like structures
                if (int.TryParse(prop.Name, out _)) continue;

                string propType = null;
                string sourcePath = $"{basePath}/{prop.Name}";

                switch (prop)
                {
                    case WzSubProperty sub:
                        string cleanName = WzReflectionUtils.SanitizeName(prop.Name);
                        string structName = WzReflectionUtils.ToPascalCase(cleanName);
                        var nestedStruct = modelBuilder.GetOrCreateStruct(structName);
                        UpdateSchemaFromNodeFiltered(sub, nestedStruct, modelBuilder, sourcePath);
                        if (nestedStruct.Properties.Any())
                        {
                            propType = structName;
                        }
                        break;
                    case WzVectorProperty: propType = "Vector2D"; break;
                    case WzStringProperty: propType = "String"; break;
                    case WzShortProperty: propType = "i16"; break;
                    case WzIntProperty:
                    case WzLongProperty:
                        propType = "i32"; break;
                    case WzFloatProperty: propType = "f32"; break;
                    case WzDoubleProperty: propType = "f64"; break;
                    case WzPngProperty: propType = "String"; break;
                }

                if (propType != null)
                {
                    parentStruct.AddProperty(new PropertyDef(WzReflectionUtils.SanitizeName(prop.Name), propType, sourcePath));
                }
            }
        }

        private Dictionary<string, object> ParsePropertyNodeFiltered(WzSubProperty propertyNode)
        {
            var dict = new Dictionary<string, object>();
            if (propertyNode == null) return dict;

            foreach (var prop in propertyNode.WzProperties)
            {
                // Skip ignored properties
                if (IgnoredProperties.Contains(prop.Name))
                    continue;

                object value = prop switch
                {
                    WzSubProperty sub => ParsePropertyNodeFiltered(sub),
                    WzVectorProperty vec => new System.Numerics.Vector2(vec.X.Value, vec.Y.Value),
                    WzStringProperty str => str.Value,
                    WzShortProperty s => s.Value,
                    WzIntProperty i => i.Value,
                    WzLongProperty l => l.Value,
                    WzFloatProperty f => f.Value,
                    WzDoubleProperty d => d.Value,
                    WzPngProperty img => System.Convert.ToBase64String(img.GetBytes()),
                    _ => null
                };
                if (value != null)
                {
                    dict[prop.Name] = value;
                }
            }
            return dict;
        }
    }
}
