using MapleLib.WzLib.Serializer.Model;
using MapleLib.WzLib.WzProperties;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MapleLib.WzLib.Serializer.Parsers
{
    public class WzMobParser : WzReflectionParser
    {
        public override string TargetFileName => "Mob.wz";

        public override bool MatchesFile(string fileName) => fileName == "Mob.wz" || fileName == "Mob2.wz";

        public override void DefineSchema(ModelBuilder modelBuilder)
        {
            var mobStruct = modelBuilder.GetOrCreateStruct("Mob");
            mobStruct.AddProperty(new PropertyDef("_id", "i32", ""));
            mobStruct.AddProperty(new PropertyDef("name", "Option<String>", ""));
            mobStruct.AddProperty(new PropertyDef("info", "Info", ""));

            // Ensure Info struct exists
            modelBuilder.GetOrCreateStruct("Info");
        }

        public override void ParseAndExportData(WzDirectory wzDir, ModelBuilder modelBuilder, string outputPath)
        {
            var allMobs = new List<Dictionary<string, object>>();
            var infoStruct = modelBuilder.GetOrCreateStruct("Info");

            foreach (var mobImg in wzDir.WzImages)
            {
                // Parse ID from image name (e.g., 100100.img -> 100100)
                string imgName = mobImg.Name.Replace(".img", "");
                if (!int.TryParse(imgName, out int mobId))
                {
                    // Debug.WriteLine($"Skipping image (not an ID): {mobImg.Name}");
                    continue;
                }

                var mobData = new Dictionary<string, object>();
                mobData["_id"] = mobId;

                // Add string data
                var name = WzStringCache.Get("mob", mobId, "name");
                if (name != null) mobData["name"] = name;

                if (mobImg["info"] is WzSubProperty infoNode)
                {
                    UpdateSchemaFromNode(infoNode, infoStruct, modelBuilder, $"{mobImg.Name}/info");
                    mobData["info"] = ParsePropertyNode(infoNode);
                }
                
                allMobs.Add(mobData);
            }
            
            Debug.WriteLine($"Total mobs processed: {allMobs.Count}");
            ExportDataToFile(allMobs, "mobs", outputPath);
        }
    }
}
