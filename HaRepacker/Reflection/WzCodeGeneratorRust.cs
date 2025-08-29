using MapleLib.WzLib.Serializer.Model;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MapleLib.WzLib.Serializer.CodeGenerators
{
    public class WzCodeGeneratorRust : IWzReflectionCodeGenerator
    {
        public string FileExtension => ".rs";

        public string GenerateCode(ModelBuilder modelBuilder)
        {
            var builder = new StringBuilder();
            builder.AppendLine("use serde::{Deserialize, Serialize};");
            builder.AppendLine("use std::collections::HashMap;");

            // Define a placeholder for Vector2D if it's used
            if (modelBuilder.Structs.Any(s => s.Properties.Any(p => p.Type == "Vector2D")))
            {
                builder.AppendLine("\n// Placeholder for a 2D vector type. Replace with your own implementation.");
                builder.AppendLine("#[derive(Serialize, Deserialize, Debug, Default, PartialEq, Clone)]\npub struct Vector2D { pub x: f32, pub y: f32 }");
            }
            
            // Root-level enums
            foreach (var enumDef in modelBuilder.Enums.OrderBy(e => e.Name))
            {
                builder.AppendLine(GenerateEnum(enumDef));
            }

            // Group structs into modules
            var groupedStructs = modelBuilder.Structs
                .Where(s => s.Properties.Any())
                .GroupBy(s => GetModuleKey(s.Name))
                .OrderBy(g => g.Key);

            foreach (var group in groupedStructs)
            {
                bool inModule = group.Key != "root";
                if (inModule)
                {
                    builder.AppendLine($"\npub mod {group.Key} {{");
                    builder.AppendLine("    use serde::{Deserialize, Serialize};");
                    if (group.Any(s => s.Properties.Any(p => p.Type == "Vector2D")))
                    {
                       builder.AppendLine("    use super::Vector2D;");
                    }
                }

                foreach (var structDef in group.OrderBy(s => s.Name))
                {
                    var sanitizedName = SanitizeStructNameForModule(structDef.Name, group.Key);
                    if (string.IsNullOrEmpty(sanitizedName)) sanitizedName = WzReflectionUtils.ToSnakeCase(structDef.Name);
                    
                    builder.AppendLine(GenerateStruct(structDef, sanitizedName, inModule));
                }

                if (inModule)
                {
                    builder.AppendLine("}");
                }
            }
            return builder.ToString();
        }

        private string GetModuleKey(string structName)
        {
            if (structName.StartsWith("ThothSearchOption")) return "thoth_search_option";
            return "root";
        }

        private string SanitizeStructNameForModule(string originalName, string moduleKey)
        {
            if (moduleKey == "thoth_search_option")
            {
                return originalName.Replace("ThothSearchOption", "");
            }
            return originalName;
        }

        private string GenerateStruct(StructDef structDef, string name, bool inModule)
        {
            var fieldsBuilder = new StringBuilder();
            string indent = inModule ? "    " : "";

            foreach (var prop in structDef.Properties)
            {
                string originalName = prop.Name;
                string snakeCaseName = WzReflectionUtils.ToSnakeCase(originalName);

                if (originalName != snakeCaseName)
                {
                    fieldsBuilder.AppendLine($"{indent}    #[serde(rename = \"{originalName}\")]");
                }

                string line = $"{indent}    pub {snakeCaseName}: {prop.Type},";
                if (!string.IsNullOrEmpty(prop.SourcePath))
                {
                    line += $" // Source: {prop.SourcePath}";
                }
                fieldsBuilder.AppendLine(line);
            }
            return $"\n#[derive(Serialize, Deserialize, Debug, Default, PartialEq, Clone)]\npub struct {name} {{\n{fieldsBuilder.ToString()}{indent}}}";
        }

        private string GenerateEnum(EnumDef enumDef)
        {
            var variants = string.Join(",\n", enumDef.Variants.Select(v => $"    {v}"));
            return $"\n#[derive(Serialize, Deserialize, Debug, PartialEq, Clone)]\npub enum {enumDef.Name} {{\n{variants},\n}}";
        }
    }
}
