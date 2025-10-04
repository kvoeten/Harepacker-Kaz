using MapleLib.WzLib.Serializer.DataModels;
using MapleLib.WzLib.Serializer.Model;
using MapleLib.WzLib.WzProperties;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace MapleLib.WzLib.Serializer.Parsers
{
    public class WzCharacterParser : WzReflectionParser
    {
        public override string TargetFileName => "Character.wz";

        public override void DefineSchema(ModelBuilder modelBuilder)
        {
            // Define main Item schema with concrete types for info and spec
            var mainItemStruct = modelBuilder.GetOrCreateStruct("Equip");
            mainItemStruct.AddProperty(new PropertyDef("id", "i32", ""));
            mainItemStruct.AddProperty(new PropertyDef("type", "ItemType", ""));
            mainItemStruct.AddProperty(new PropertyDef("info", "Option<Info>", ""));
            mainItemStruct.AddProperty(new PropertyDef("spec", "Option<Spec>", ""));

            modelBuilder.GetOrCreateEnum("EquipType");

            // Pre-define the superset structs for item info and specs.
            // Properties will be discovered and added during the parsing phase.
            modelBuilder.GetOrCreateStruct("Info");
            modelBuilder.GetOrCreateStruct("Spec");
        }

        public override void ParseAndExportData(WzDirectory wzDir, ModelBuilder modelBuilder, string outputPath)
        {
            var allItems = new List<ItemData>();
            var equipTypeEnum = modelBuilder.GetOrCreateEnum("EquipType");

            var infoStruct = modelBuilder.GetOrCreateStruct("Info");
            var specStruct = modelBuilder.GetOrCreateStruct("Spec");

            // This dictionary maps property names to the structs they should populate.
            var supersetStructs = new Dictionary<string, StructDef>
            {
                { "info", infoStruct },
                { "spec", specStruct }
            };

            foreach (var categoryDir in wzDir.WzDirectories)
            {
                equipTypeEnum.AddVariant(categoryDir.Name);
                foreach (WzImage itemImg in categoryDir.WzImages)
                {
                    // Verify ID
                    if (!int.TryParse(itemImg.Name, out int itemId)) continue;
                    var itemData = new ItemData { Id = itemId, ItemType = "Equip", EquipType = categoryDir.Name };

                    // Iterate through the item's properties (like "info", "spec")
                    // and merge them into the correct superset struct.
                    foreach (var prop in itemImg.WzProperties)
                    {
                        if (prop is WzSubProperty subProp && supersetStructs.TryGetValue(prop.Name, out var targetStruct))
                        {
                            // 1. Update the schema by discovering properties within this node.
                            UpdateSchemaFromNode(subProp, targetStruct, modelBuilder, $"{categoryDir.Name}/{categoryDir.Name}/{itemImg.Name}/{prop.Name}");

                            // 2. Parse the data for this section.
                            if (prop.Name == "info")
                            {
                                itemData.Info = ParsePropertyNode(subProp);
                            }
                            else if (prop.Name == "spec")
                            {
                                itemData.Spec = ParsePropertyNode(subProp);
                            }
                        }
                    }

                    // Add props to item data
                    allItems.Add(itemData);
                }
            }

            ExportDataToFile(allItems, "equip", outputPath);
        }
    }
}
