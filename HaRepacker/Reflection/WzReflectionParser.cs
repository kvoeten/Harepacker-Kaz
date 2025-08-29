using MapleLib.WzLib.Serializer.Model;
using MapleLib.WzLib.WzProperties;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using System.Collections.Generic;
using System.IO;
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
