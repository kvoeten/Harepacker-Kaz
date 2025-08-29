using MapleLib.WzLib.Serializer.DataModels;
using MapleLib.WzLib.Serializer.Model;
using MapleLib.WzLib.WzProperties;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MapleLib.WzLib.Serializer.Parsers
{
    public class WzItemParser : WzReflectionParser
    {
        public override string TargetFileName => "Item.wz";

        public override void DefineSchema(ModelBuilder modelBuilder)
        {
            // Define main Item schema
            var mainItemStruct = modelBuilder.GetOrCreateStruct("Item");
            mainItemStruct.AddProperty(new PropertyDef("id", "i32", ""));
            mainItemStruct.AddProperty(new PropertyDef("item_type", "ItemType", ""));
            mainItemStruct.AddProperty(new PropertyDef("info", "HashMap<String, serde_json::Value>", ""));
            mainItemStruct.AddProperty(new PropertyDef("spec", "HashMap<String, serde_json::Value>", ""));

            modelBuilder.GetOrCreateEnum("ItemType");

            // Define ThothSearchOption schema
            DefineThothSearchOptionSchema(modelBuilder);
        }

        public override void ParseAndExportData(WzDirectory wzDir, string outputPath)
        {
            var allItems = new List<ItemData>();
            var itemTypeEnum = new EnumDef("ItemType");

            // Parse all regular items
            foreach (var categoryDir in wzDir.WzDirectories)
            {
                itemTypeEnum.AddVariant(CultureInfo.CurrentCulture.TextInfo.ToTitleCase(categoryDir.Name));
                foreach (WzImage itemImage in categoryDir.WzImages)
                {
                    // The image file name itself is the item ID (e.g., "05010000.img")
                    string potentialId = itemImage.Name.Replace(".img", "");
                    if (!int.TryParse(potentialId, out int itemId))
                    {
                        // Skip non-item images like ThothSearchOption.img, which is handled separately
                        continue;
                    }

                    var itemData = new ItemData
                    {
                        Id = itemId,
                        ItemType = categoryDir.Name
                    };

                    // Directly access properties from the itemImage
                    if (itemImage["info"] is WzSubProperty infoNode)
                    {
                        itemData.Info = ParsePropertyNode(infoNode);
                    }
                    if (itemImage["spec"] is WzSubProperty specNode)
                    {
                        itemData.Spec = ParsePropertyNode(specNode);
                    }
                    allItems.Add(itemData);
                }
            }

            // Export all item data
            ExportDataToFile(allItems, "items", outputPath);

            // Find and parse ThothSearchOption.img specifically
            if (wzDir.WzImages.FirstOrDefault(i => i.Name == "ThothSearchOption.img") is WzImage thothImg)
            {
                ParseThothSearchOption(thothImg, outputPath);
            }
        }

        #region Thoth Search Option Logic

        private void DefineThothSearchOptionSchema(ModelBuilder mb)
        {
            var mainStruct = mb.GetOrCreateStruct("ThothSearchOption");
            mainStruct.AddProperty(new PropertyDef("hot", "Vec<thoth_search_option::Option>", ""));
            mainStruct.AddProperty(new PropertyDef("regular", "Vec<thoth_search_option::Option>", ""));
            mainStruct.AddProperty(new PropertyDef("item_category", "Vec<thoth_search_option::ItemCategory>", ""));
            mainStruct.AddProperty(new PropertyDef("default_settings", "Vec<thoth_search_option::DefaultSetting>", ""));

            var optionStruct = mb.GetOrCreateStruct("ThothSearchOptionOption");
            optionStruct.AddProperty(new PropertyDef("name", "String", ""));
            optionStruct.AddProperty(new PropertyDef("string", "String", ""));
            optionStruct.AddProperty(new PropertyDef("index", "i16", ""));

            var itemCategoryStruct = mb.GetOrCreateStruct("ThothSearchOptionItemCategory");
            itemCategoryStruct.AddProperty(new PropertyDef("name", "String", ""));
            itemCategoryStruct.AddProperty(new PropertyDef("item_detail_category", "Vec<thoth_search_option::ItemDetailCategory>", ""));

            var itemDetailStruct = mb.GetOrCreateStruct("ThothSearchOptionItemDetailCategory");
            itemDetailStruct.AddProperty(new PropertyDef("name", "String", ""));
            itemDetailStruct.AddProperty(new PropertyDef("string", "String", ""));
            itemDetailStruct.AddProperty(new PropertyDef("begin", "i32", ""));
            itemDetailStruct.AddProperty(new PropertyDef("end", "i32", ""));

            var defaultSettingStruct = mb.GetOrCreateStruct("ThothSearchOptionDefaultSetting");
            defaultSettingStruct.AddProperty(new PropertyDef("job_name", "String", ""));
            defaultSettingStruct.AddProperty(new PropertyDef("weapon", "String", ""));
            defaultSettingStruct.AddProperty(new PropertyDef("option", "String", ""));
            defaultSettingStruct.AddProperty(new PropertyDef("job", "i32", ""));
        }

        private void ParseThothSearchOption(WzImage img, string outputPath)
        {
            var data = new ThothSearchOptionData();

            if (img["Option"] is WzSubProperty optionRoot)
            {
                if (optionRoot["Hot"] is WzSubProperty hotOptions)
                    data.Hot = hotOptions.WzProperties.Select(p => new ThothSearchOptionOptionData 
                    { 
                        Name = p.Name, 
                        DisplayString = WzReflectionUtils.GetWzValue(p, "string", ""), 
                        Index = WzReflectionUtils.GetWzValue(p, "index", (short)0) 
                    }).ToList();

                if (optionRoot["Normal"] is WzSubProperty normalOptions)
                    data.Regular = normalOptions.WzProperties.Select(p => new ThothSearchOptionOptionData 
                    { 
                        Name = p.Name, 
                        DisplayString = WzReflectionUtils.GetWzValue(p, "string", ""), 
                        Index = WzReflectionUtils.GetWzValue(p, "index", (short)0) 
                    }).ToList();
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
                        data.DefaultSettings.Add(new ThothSearchOptionDefaultSettingData { 
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

