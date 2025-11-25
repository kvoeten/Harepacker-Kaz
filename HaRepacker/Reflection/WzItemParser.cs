using MapleLib.WzLib.Serializer.DataModels;
using MapleLib.WzLib.Serializer.Model;
using MapleLib.WzLib.WzProperties;
using System.Collections.Generic;
using System.Linq;

namespace MapleLib.WzLib.Serializer.Parsers
{
    public class WzItemParser : WzReflectionParser
    {
        public override string TargetFileName => "Item.wz";

        public override void DefineSchema(ModelBuilder modelBuilder)
        {
            // Define main Item schema with concrete types for info and spec
            var mainItemStruct = modelBuilder.GetOrCreateStruct("Item");
            mainItemStruct.AddProperty(new PropertyDef("_id", "i32", ""));
            mainItemStruct.AddProperty(new PropertyDef("type", "ItemType", ""));
            mainItemStruct.AddProperty(new PropertyDef("name", "Option<String>", ""));
            mainItemStruct.AddProperty(new PropertyDef("desc", "Option<String>", ""));
            mainItemStruct.AddProperty(new PropertyDef("icon", "Option<String>", ""));
            mainItemStruct.AddProperty(new PropertyDef("sample", "Option<String>", ""));
            mainItemStruct.AddProperty(new PropertyDef("info", "Option<Info>", ""));
            mainItemStruct.AddProperty(new PropertyDef("spec", "Option<Spec>", ""));

            modelBuilder.GetOrCreateEnum("ItemType");

            // Pre-define the superset structs for item info and specs.
            // Properties will be discovered and added during the parsing phase.
            modelBuilder.GetOrCreateStruct("Info");
            modelBuilder.GetOrCreateStruct("Spec");

            // Define ThothSearchOption schema, as it's specific to Item.wz
            DefineThothSearchOptionSchema(modelBuilder);
        }

        public override void ParseAndExportData(WzDirectory wzDir, ModelBuilder modelBuilder, string outputPath)
        {
            var allItems = new List<ItemData>();
            var itemTypeEnum = modelBuilder.GetOrCreateEnum("ItemType");

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
                itemTypeEnum.AddVariant(categoryDir.Name);
                foreach (WzImage itemSubCategoryImg in categoryDir.WzImages)
                {
                    foreach (WzSubProperty itemNode in itemSubCategoryImg.WzProperties.OfType<WzSubProperty>())
                    {
                        if (!int.TryParse(itemNode.Name, out int itemId)) continue;

                        var itemData = new ItemData { Id = itemId, ItemType = categoryDir.Name };

                        // Iterate through the item's properties (like "info", "spec")
                        // and merge them into the correct superset struct.
                        foreach (var prop in itemNode.WzProperties)
                        {
                            if (prop is WzSubProperty subProp && supersetStructs.TryGetValue(prop.Name, out var targetStruct))
                            {
                                // 1. Update the schema by discovering properties within this node.
                                UpdateSchemaFromNode(subProp, targetStruct, modelBuilder, $"{categoryDir.Name}/{itemSubCategoryImg.Name}/{itemNode.Name}/{prop.Name}");

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

                        // Add string data
                        var name = WzStringCache.Get("item", itemId, "name");
                        var desc = WzStringCache.Get("item", itemId, "desc");
                        if (name != null) itemData.Name = name;
                        if (desc != null) itemData.Desc = desc;

                        // Extract images
                        var icon = ExtractImageAsBase64(itemNode, "info/icon");
                        var sample = ExtractImageAsBase64(itemNode, "info/sample");
                        if (icon != null) itemData.Icon = icon;
                        if (sample != null) itemData.Sample = sample;

                        allItems.Add(itemData);
                    }
                }
            }

            ExportDataToFile(allItems, "items", outputPath);

            // Parse ThothSearchOption as it is a special case within Item.wz
            var thothImage = wzDir.WzImages.FirstOrDefault(img => img.Name == "ThothSearchOption.img");
            if (thothImage != null)
            {
                ParseThothSearchOption(thothImage, outputPath);
            }
        }

        #region ThothSearchOption Specific Parsing
        private void DefineThothSearchOptionSchema(ModelBuilder modelBuilder)
        {
            var root = modelBuilder.GetOrCreateStruct("ThothSearchOption");
            root.AddProperty(new PropertyDef("hot", "Vec<ThothSearchOptionOption>", ""));
            root.AddProperty(new PropertyDef("regular", "Vec<ThothSearchOptionOption>", ""));
            root.AddProperty(new PropertyDef("item_category", "Vec<ThothSearchOptionItemCategory>", ""));
            root.AddProperty(new PropertyDef("default_settings", "Vec<ThothSearchOptionDefaultSetting>", ""));

            var option = modelBuilder.GetOrCreateStruct("ThothSearchOptionOption");
            option.AddProperty(new PropertyDef("name", "String", ""));
            option.AddProperty(new PropertyDef("string", "String", ""));
            option.AddProperty(new PropertyDef("index", "i16", ""));

            var category = modelBuilder.GetOrCreateStruct("ThothSearchOptionItemCategory");
            category.AddProperty(new PropertyDef("name", "String", ""));
            category.AddProperty(new PropertyDef("item_detail_category", "Vec<ThothSearchOptionItemDetailCategory>", ""));

            var detailCategory = modelBuilder.GetOrCreateStruct("ThothSearchOptionItemDetailCategory");
            detailCategory.AddProperty(new PropertyDef("name", "String", ""));
            detailCategory.AddProperty(new PropertyDef("string", "String", ""));
            detailCategory.AddProperty(new PropertyDef("begin", "i32", ""));
            detailCategory.AddProperty(new PropertyDef("end", "i32", ""));

            var defaultSetting = modelBuilder.GetOrCreateStruct("ThothSearchOptionDefaultSetting");
            defaultSetting.AddProperty(new PropertyDef("job_name", "String", ""));
            defaultSetting.AddProperty(new PropertyDef("weapon", "String", ""));
            defaultSetting.AddProperty(new PropertyDef("option", "String", ""));
            defaultSetting.AddProperty(new PropertyDef("job", "i32", ""));
        }

        private void ParseThothSearchOption(WzImage img, string outputPath)
        {
            var data = new ThothSearchOptionData();

            if (img["Option"] is WzSubProperty optionRoot)
            {
                if (optionRoot["Hot"] is WzSubProperty hotOptions)
                    data.Hot = hotOptions.WzProperties.Select(p => new ThothSearchOptionOptionData { Name = p.Name, DisplayString = WzReflectionUtils.GetWzValue(p, "string", ""), Index = WzReflectionUtils.GetWzValue(p, "index", (short)0) }).ToList();

                if (optionRoot["Normal"] is WzSubProperty regularOptions)
                    data.Regular = regularOptions.WzProperties.Select(p => new ThothSearchOptionOptionData { Name = p.Name, DisplayString = WzReflectionUtils.GetWzValue(p, "string", ""), Index = WzReflectionUtils.GetWzValue(p, "index", (short)0) }).ToList();
            }

            if (img["ItemCategory"] is WzSubProperty itemCategoryRoot && img["ItemDetailCategory"] is WzSubProperty itemDetailCategoryRoot)
            {
                foreach (WzSubProperty category in itemCategoryRoot.WzProperties.OfType<WzSubProperty>())
                {
                    var categoryData = new ThothSearchOptionItemCategoryData { Name = category.Name };
                    if (itemDetailCategoryRoot[category.Name] is WzSubProperty detailRoot)
                    {
                        categoryData.ItemDetailCategory = detailRoot.WzProperties.Select(p => new ThothSearchOptionItemDetailCategoryData
                        {
                            Name = p.Name,
                            DisplayString = WzReflectionUtils.GetWzValue(p, "string", ""),
                            Begin = WzReflectionUtils.GetWzValue(p, "begin", 0),
                            End = WzReflectionUtils.GetWzValue(p, "end", 0)
                        }).ToList();
                    }
                    data.ItemCategory.Add(categoryData);
                }
            }

            if (img["DefaultSetting"] is WzSubProperty defaultSettingRoot)
            {
                foreach (WzSubProperty jobType in defaultSettingRoot.WzProperties.OfType<WzSubProperty>())
                {
                    foreach (WzSubProperty job in jobType.WzProperties.OfType<WzSubProperty>())
                    {
                        data.DefaultSettings.Add(new ThothSearchOptionDefaultSettingData
                        {
                            JobName = job.Name,
                            Weapon = WzReflectionUtils.GetWzValue(job, "weapon", ""),
                            Option = WzReflectionUtils.GetWzValue(job, "option", ""),
                            Job = WzReflectionUtils.GetWzValue(job, "job", 0)
                        });
                    }
                }
            }

            ExportDataToFile(data, "thoth_search_option", outputPath);
        }
        #endregion
    }
}
