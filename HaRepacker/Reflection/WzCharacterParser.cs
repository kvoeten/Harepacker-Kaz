using MapleLib.WzLib.Serializer.DataModels;
using MapleLib.WzLib.Serializer.Model;
using MapleLib.WzLib.WzProperties;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Drawing.Imaging;
using System.IO;

namespace MapleLib.WzLib.Serializer.Parsers
{
    public class WzCharacterParser : WzReflectionParser
    {
        public override string TargetFileName => "Character.wz";

        public override void DefineSchema(ModelBuilder modelBuilder)
        {
            // Define main Item schema with concrete types for info and spec
            var mainItemStruct = modelBuilder.GetOrCreateStruct("Equip");
            mainItemStruct.AddProperty(new PropertyDef("_id", "i32", ""));
            mainItemStruct.AddProperty(new PropertyDef("EquipType", "EquipType", ""));
            mainItemStruct.AddProperty(new PropertyDef("name", "Option<String>", ""));
            mainItemStruct.AddProperty(new PropertyDef("desc", "Option<String>", ""));
            mainItemStruct.AddProperty(new PropertyDef("icon", "Option<String>", ""));
            mainItemStruct.AddProperty(new PropertyDef("info", "Option<Info>", ""));
            mainItemStruct.AddProperty(new PropertyDef("spec", "Option<Spec>", ""));

            modelBuilder.GetOrCreateEnum("EquipType");

            // Pre-define the superset structs for item info and specs.
            // Properties will be discovered and added during the parsing phase.
            modelBuilder.GetOrCreateStruct("Info");
            modelBuilder.GetOrCreateStruct("Spec");

            // Define RenderData schema
            var renderDataStruct = modelBuilder.GetOrCreateStruct("RenderData");
            renderDataStruct.AddProperty(new PropertyDef("stand1", "Option<Map<String, RenderNode>>", ""));
            renderDataStruct.AddProperty(new PropertyDef("default", "Option<Map<String, RenderNode>>", ""));

            var renderNodeStruct = modelBuilder.GetOrCreateStruct("RenderNode");
            renderNodeStruct.AddProperty(new PropertyDef("image", "String", ""));
            renderNodeStruct.AddProperty(new PropertyDef("origin", "Vector2D", ""));
            renderNodeStruct.AddProperty(new PropertyDef("z", "String", ""));

            var vector2DStruct = modelBuilder.GetOrCreateStruct("Vector2D");
            vector2DStruct.AddProperty(new PropertyDef("x", "i32", ""));
            vector2DStruct.AddProperty(new PropertyDef("y", "i32", ""));

            mainItemStruct.AddProperty(new PropertyDef("render", "Option<RenderData>", ""));
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
                    string imgName = itemImg.Name.Replace(".img", "");
                    if (!int.TryParse(imgName, out int itemId)) continue;
                    var itemData = new ItemData { Id = itemId, ItemType = "Equip", EquipType = categoryDir.Name };

                    // Iterate through the item's properties (like "info", "spec")
                    // and merge them into the correct superset struct.
                    foreach (var prop in itemImg.WzProperties)
                    {
                        if (prop is WzSubProperty subProp && supersetStructs.TryGetValue(prop.Name, out var targetStruct))
                        {
                            // Update the schema by discovering properties within this node.
                            UpdateSchemaFromNode(subProp, targetStruct, modelBuilder, $"{categoryDir.Name}/{categoryDir.Name}/{itemImg.Name}/{prop.Name}");

                            // Parse the data for this section.
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

                    // Extract icon image
                    var icon = ExtractImageAsBase64(itemImg, "info/icon");
                    if (icon != null) itemData.Icon = icon;

                    // Extract Render Data
                    itemData.Render = ExtractRenderData(itemImg);

                    // Add props to item data
                    allItems.Add(itemData);
                }
            }

            ExportDataToFile(allItems, "equip", outputPath);
        }

        private RenderData ExtractRenderData(WzImage itemImg)
        {
            var renderData = new RenderData();

            // 1. Handle "stand1" -> "0" -> canvas
            if (itemImg["stand1"] is WzSubProperty stand1Prop)
            {
                if (stand1Prop["0"] is WzSubProperty frame0)
                {
                    foreach (var prop in frame0.WzProperties)
                    {
                        if (prop is WzCanvasProperty canvasProp)
                        {
                            var renderNode = ExtractRenderNode(canvasProp);
                            if (renderNode != null)
                            {
                                renderData.Stand1[prop.Name] = renderNode;
                            }
                        }
                    }
                }
            }

            // 2. Handle "default" -> canvas
            if (itemImg["default"] is WzSubProperty defaultProp)
            {
                foreach (var prop in defaultProp.WzProperties)
                {
                    if (prop is WzCanvasProperty canvasProp)
                    {
                        var renderNode = ExtractRenderNode(canvasProp);
                        if (renderNode != null)
                        {
                            renderData.Default[prop.Name] = renderNode;
                        }
                    }
                }
            }

            return renderData;
        }

        private RenderNode ExtractRenderNode(WzCanvasProperty canvasProp)
        {
            var node = new RenderNode();

            // Extract Image
            if (canvasProp.PngProperty != null)
            {
                using (var bitmap = canvasProp.PngProperty.GetBitmap())
                {
                    if (bitmap != null)
                    {
                        using (var stream = new MemoryStream())
                        {
                            bitmap.Save(stream, ImageFormat.Png);
                            var bytes = stream.ToArray();
                            node.Image = System.Convert.ToBase64String(bytes);
                        }
                    }
                }
            }

            // Extract Properties
            foreach (var subProp in canvasProp.WzProperties)
            {
                if (subProp.Name == "origin" && subProp is WzVectorProperty originVec)
                {
                    node.Origin = new Vector2D { X = originVec.X.Value, Y = originVec.Y.Value };
                }
                else if (subProp.Name == "z" && subProp is WzStringProperty zStr)
                {
                    node.Z = zStr.Value;
                }
                else
                {
                    // Dynamic extraction for other properties
                    object val = null;
                    if (subProp is WzVectorProperty vec) val = new Vector2D { X = vec.X.Value, Y = vec.Y.Value };
                    else if (subProp is WzStringProperty str) val = str.Value;
                    else if (subProp is WzIntProperty i) val = i.Value;
                    else if (subProp is WzShortProperty s) val = s.Value;
                    else if (subProp is WzLongProperty l) val = l.Value;
                    else if (subProp is WzFloatProperty f) val = f.Value;
                    else if (subProp is WzDoubleProperty d) val = d.Value;

                    if (val != null)
                    {
                        node.OtherProperties[subProp.Name] = val;
                    }
                }
            }

            return node;
        }
    }
}
