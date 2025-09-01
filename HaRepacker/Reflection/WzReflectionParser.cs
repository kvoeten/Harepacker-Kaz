using MapleLib.WzLib;
using MapleLib.WzLib.Serializer.Model;
using MapleLib.WzLib.WzProperties;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace MapleLib.WzLib.Serializer.Parsers
{
    /// <summary>
    /// An abstract base class for WZ file parsers that provides common utility methods.
    /// </summary>
    public abstract class WzReflectionParser : IWzReflectionFileParser
    {
        // Abstract properties and methods to be implemented by concrete parsers
        public abstract string TargetFileName { get; }
        public abstract void DefineSchema(ModelBuilder modelBuilder);
        public abstract void ParseAndExportData(WzDirectory wzDir, ModelBuilder modelBuilder, string outputPath);

        /// <summary>
        /// Recursively parses a WzSubProperty node into a dictionary suitable for serialization.
        /// </summary>
        protected Dictionary<string, object> ParsePropertyNode(WzSubProperty propertyNode)
        {
            var dict = new Dictionary<string, object>();
            if (propertyNode == null) return dict;

            foreach (var prop in propertyNode.WzProperties)
            {
                object value = prop switch
                {
                    WzSubProperty sub => ParsePropertyNode(sub),
                    WzVectorProperty vec => new Vector2(vec.X.Value, vec.Y.Value),
                    WzStringProperty str => str.Value,
                    WzShortProperty s => s.Value,
                    WzIntProperty i => i.Value,
                    WzLongProperty l => l.Value,
                    WzFloatProperty f => f.Value,
                    WzDoubleProperty d => d.Value,
                    WzPngProperty => "[Image Data]", // Placeholder for binary data
                    _ => null
                };
                if (value != null)
                {
                    dict[prop.Name] = value;
                }
            }
            return dict;
        }

        /// <summary>
        /// Recursively discovers properties from a WZ node and adds them to the schema in the ModelBuilder.
        /// This is a generic method that can be used by any parser.
        /// </summary>
        protected void UpdateSchemaFromNode(WzSubProperty propertyNode, StructDef parentStruct, ModelBuilder modelBuilder, string basePath)
        {
            if (propertyNode == null) return;

            foreach (var prop in propertyNode.WzProperties)
            {
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
                        UpdateSchemaFromNode(sub, nestedStruct, modelBuilder, sourcePath);
                        // Only add the property if the nested struct is not empty, otherwise it's likely just a container
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
                    case WzPngProperty: propType = "serde_json::Value"; break; // Treat images as generic JSON values
                }

                if (propType != null)
                {
                    parentStruct.AddProperty(new PropertyDef(WzReflectionUtils.SanitizeName(prop.Name), propType, sourcePath));
                }
            }
        }

        /// <summary>
        /// Helper method to iterate over all WzImage and WzDirectory objects recursively.
        /// </summary>
        protected IEnumerable<WzObject> GetAllChildren(WzDirectory startDir)
        {
            foreach (WzImage img in startDir.WzImages)
            {
                yield return img;
            }

            foreach (WzDirectory subDir in startDir.WzDirectories)
            {
                yield return subDir; // Yield the directory itself

                // Recurse and yield the children of the subdirectory
                foreach (var grandChild in GetAllChildren(subDir))
                {
                    yield return grandChild;
                }
            }
        }


        /// <summary>
        /// Serializes the given data object to both BSON and JSON files.
        /// </summary>
        protected void ExportDataToFile(object data, string fileName, string basePath)
        {
            // BSON Export
            string bsonDir = Path.Combine(Path.GetDirectoryName(basePath), "bson_data");
            Directory.CreateDirectory(bsonDir);
            string bsonPath = Path.Combine(bsonDir, $"{fileName}.bson");

            using (var fs = new FileStream(bsonPath, FileMode.Create))
            using (var writer = new BsonDataWriter(fs))
            {
                var serializer = new JsonSerializer();
                serializer.Serialize(writer, data);
            }

            // JSON Export for verification
            string jsonDir = Path.Combine(Path.GetDirectoryName(basePath), "json_data");
            Directory.CreateDirectory(jsonDir);
            string jsonPath = Path.Combine(jsonDir, $"{fileName}.json");
            File.WriteAllText(jsonPath, JsonConvert.SerializeObject(data, Formatting.Indented));
        }
    }
}

