using MapleLib.WzLib;
using MapleLib.WzLib.Serializer.Model;
using MapleLib.WzLib.WzProperties;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MapleLib.WzLib.Serializer.Parsers
{
    /// <summary>
    /// A dynamic parser for Etc.wz. It distinguishes between top-level directories (categories)
    /// and top-level images (standalone entries) to build an accurate data schema.
    /// </summary>
    public class WzEtcParser : WzReflectionParser
    {
        public override string TargetFileName => "Etc.wz";

        public override void DefineSchema(ModelBuilder modelBuilder)
        {
            // Schema is generated dynamically during the data parsing phase.
        }

        public override void ParseAndExportData(WzDirectory wzDir, ModelBuilder modelBuilder, string outputPath)
        {
            // Process top-level directories as categories of items
            foreach (var categoryDir in wzDir.WzDirectories)
            {
                ProcessCategoryDirectory(categoryDir, modelBuilder, outputPath);
            }

            // Process top-level images as standalone data structures
            foreach (var image in wzDir.WzImages)
            {
                ProcessStandaloneImage(image, modelBuilder, outputPath);
            }
        }

        /// <summary>
        /// Processes a directory as a collection of items sharing a common schema.
        /// </summary>
        private void ProcessCategoryDirectory(WzDirectory categoryDir, ModelBuilder modelBuilder, string outputPath)
        {
            string categoryName = WzReflectionUtils.ToPascalCase(categoryDir.Name); // e.g., "Android"
            var entryStruct = modelBuilder.GetOrCreateStruct(categoryName);
            entryStruct.AddProperty(new PropertyDef("id", "i32", ""));

            var allEntriesData = new List<Dictionary<string, object>>();
            var supersetStructs = new Dictionary<string, StructDef>();

            // Iterate over all .img files in the directory to build a complete "superset" schema
            foreach (var image in categoryDir.WzImages.OfType<WzImage>())
            {
                if (!int.TryParse(Path.GetFileNameWithoutExtension(image.Name), out int imageId)) continue;

                var currentEntryData = new Dictionary<string, object> { { "id", imageId } };

                foreach (var prop in image.WzProperties.OfType<WzSubProperty>())
                {
                    // Define nested struct name, e.g., "AndroidInfo"
                    string nestedStructName = categoryName + WzReflectionUtils.ToPascalCase(prop.Name);

                    // Get or create the superset struct for this property type
                    if (!supersetStructs.TryGetValue(prop.Name, out var targetStruct))
                    {
                        targetStruct = modelBuilder.GetOrCreateStruct(nestedStructName);
                        supersetStructs[prop.Name] = targetStruct;

                        // Add property to the main entry struct, e.g., pub info: Option<AndroidInfo>
                        entryStruct.AddProperty(new PropertyDef(prop.Name, $"Option<{nestedStructName}>", ""));
                    }

                    // Discover/update the schema from this node's properties
                    UpdateSchemaRecursively(prop, targetStruct, modelBuilder, $"{categoryDir.Name}/{image.Name}/{prop.Name}", categoryName);

                    // Parse the data for this property
                    currentEntryData[prop.Name] = ParsePropertyNode(prop);
                }
                allEntriesData.Add(currentEntryData);
            }

            // Export all parsed data for this category to a single file
            ExportDataToFile(allEntriesData, WzReflectionUtils.ToSnakeCase(categoryDir.Name), outputPath);
        }

        /// <summary>
        /// Processes a single .img file at the root as a standalone data structure.
        /// </summary>
        private void ProcessStandaloneImage(WzImage image, ModelBuilder modelBuilder, string outputPath)
        {
            string entryName = Path.GetFileNameWithoutExtension(image.Name);
            string structName = WzReflectionUtils.ToPascalCase(WzReflectionUtils.SanitizeName(entryName));
            var rootStruct = modelBuilder.GetOrCreateStruct(structName);

            var container = new WzSubProperty(image.Name);
            container.AddProperties(image.WzProperties);

            UpdateSchemaRecursively(container, rootStruct, modelBuilder, entryName, "");

            if (rootStruct.Properties.Any())
            {
                var data = ParsePropertyNode(container);
                ExportDataToFile(data, WzReflectionUtils.ToSnakeCase(entryName), outputPath);
            }
        }

        /// <summary>
        /// Custom recursive schema discovery method for Etc.wz that handles prefixed struct names.
        /// </summary>
        private void UpdateSchemaRecursively(WzSubProperty propertyNode, StructDef parentStruct, ModelBuilder modelBuilder, string basePath, string namePrefix)
        {
            if (propertyNode == null) return;

            foreach (var prop in propertyNode.WzProperties)
            {
                if (int.TryParse(prop.Name, out _)) continue; // Skip numeric keys

                string propType = GetPropertyTypeString(prop);
                string sourcePath = $"{basePath}/{prop.Name}";

                if (prop is WzSubProperty sub)
                {
                    string cleanName = WzReflectionUtils.SanitizeName(prop.Name);
                    // Use prefix for nested structs to avoid name collisions, e.g., "Android" + "Info"
                    string structName = namePrefix + WzReflectionUtils.ToPascalCase(cleanName);
                    var nestedStruct = modelBuilder.GetOrCreateStruct(structName);

                    UpdateSchemaRecursively(sub, nestedStruct, modelBuilder, sourcePath, namePrefix);

                    if (nestedStruct.Properties.Any())
                    {
                        propType = structName;
                    }
                }

                if (propType != null)
                {
                    parentStruct.AddProperty(new PropertyDef(prop.Name, propType, sourcePath));
                }
            }
        }

        private string GetPropertyTypeString(WzImageProperty prop)
        {
            return prop switch
            {
                WzVectorProperty => "Vector2D",
                WzStringProperty => "String",
                WzShortProperty => "i16",
                WzIntProperty => "i32",
                WzLongProperty => "i32",
                WzFloatProperty => "f32",
                WzDoubleProperty => "f64",
                _ => null
            };
        }
    }
}

