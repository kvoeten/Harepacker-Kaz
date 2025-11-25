using MapleLib.WzLib.Serializer.Model;
using MapleLib.WzLib.WzProperties;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MapleLib.WzLib.Serializer.Parsers
{
    public class WzReactorParser : WzReflectionParser
    {
        public override string TargetFileName => "Reactor.wz";

        public override void DefineSchema(ModelBuilder modelBuilder)
        {
            var reactorStruct = modelBuilder.GetOrCreateStruct("Reactor");
            reactorStruct.AddProperty(new PropertyDef("_id", "i32", ""));
            reactorStruct.AddProperty(new PropertyDef("icon", "Option<String>", ""));
            reactorStruct.AddProperty(new PropertyDef("info", "Info", ""));

            // Ensure Info struct exists
            modelBuilder.GetOrCreateStruct("Info");
        }

        public override void ParseAndExportData(WzDirectory wzDir, ModelBuilder modelBuilder, string outputPath)
        {
            var allReactors = new List<Dictionary<string, object>>();
            var infoStruct = modelBuilder.GetOrCreateStruct("Info");

            foreach (var reactorImg in wzDir.WzImages)
            {
                // Parse ID from image name (e.g., 0000000.img -> 0)
                string imgName = reactorImg.Name.Replace(".img", "");
                if (!int.TryParse(imgName, out int reactorId))
                {
                    // Debug.WriteLine($"Skipping image (not an ID): {reactorImg.Name}");
                    continue;
                }

                var reactorData = new Dictionary<string, object>();
                reactorData["_id"] = reactorId;

                if (reactorImg["info"] is WzSubProperty infoNode)
                {
                    UpdateSchemaFromNode(infoNode, infoStruct, modelBuilder, $"{reactorImg.Name}/info");
                    reactorData["info"] = ParsePropertyNode(infoNode);
                }

                // Extract reactor icon image (0/0 path)
                var icon = ExtractImageAsBase64(reactorImg, "0/0");
                if (icon != null) reactorData["icon"] = icon;
                
                allReactors.Add(reactorData);
            }
            
            Debug.WriteLine($"Total reactors processed: {allReactors.Count}");
            ExportDataToFile(allReactors, "reactors", outputPath);
        }
    }
}
