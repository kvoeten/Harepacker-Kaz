using MapleLib.WzLib.Serializer.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MapleLib.WzLib.Serializer.CodeGenerators
{
    public class WzCodeGeneratorRust : IWzReflectionCodeGenerator
    {
        public string FileExtension => ".rs";

        // A set of Rust primitives and standard library types that should not be namespace-qualified.
        private static readonly HashSet<string> Primitives = new HashSet<string>
        {
            "i16", "i32", "i64", "f32", "f64", "bool", "String", "Vector2D", "serde_json::Value"
        };

        public string GenerateCode(ModelBuilder modelBuilder)
        {
            try
            {
                var builder = new StringBuilder();
                builder.AppendLine("use serde::{Deserialize, Serialize};");
                builder.AppendLine("use std::collections::HashMap;");

                var enumsToBuild = modelBuilder.Enums.Where(e => e.Variants.Any()).OrderBy(e => e.Name).ToList();
                var structsToBuild = modelBuilder.Structs.Where(s => s.Properties.Any()).OrderBy(s => s.Name).ToList();

                if (structsToBuild.Any(s => s.Properties.Any(p => p.Type == "Vector2D")))
                {
                    builder.AppendLine("\n// Placeholder for a 2D vector type. Replace with your own implementation.");
                    builder.AppendLine("#[derive(Serialize, Deserialize, Debug, Default, PartialEq, Clone)]\npub struct Vector2D { pub x: f32, pub y: f32 }");
                }

                foreach (var enumDef in enumsToBuild)
                {
                    builder.AppendLine(GenerateEnum(enumDef));
                }

                var rootStructs = structsToBuild.Where(s => GetModuleKey(s.Name) == "root").ToList();
                var thothStructs = structsToBuild.Where(s => GetModuleKey(s.Name) == "thoth_search_option").ToList();

                foreach (var structDef in rootStructs)
                {
                    builder.AppendLine(GenerateStruct(structDef, false));
                }

                if (thothStructs.Any())
                {
                    builder.AppendLine($"\npub mod thoth_search_option {{");
                    builder.AppendLine("    use serde::{Deserialize, Serialize};");
                    if (thothStructs.Any(s => s.Properties.Any(p => p.Type.Contains("Vector2D"))))
                    {
                        builder.AppendLine("    use crate::Vector2D;");
                    }

                    foreach (var structDef in thothStructs)
                    {
                        builder.AppendLine(GenerateStruct(structDef, true));
                    }

                    builder.AppendLine("}");
                }

                return builder.ToString();
            }
            catch (Exception ex)
            {
                string errorMessage = $"// AN ERROR OCCURRED DURING CODE GENERATION:\n// {ex.Message}\n// {ex.StackTrace}";
                Debug.WriteLine(errorMessage);
                return errorMessage;
            }
        }

        private string GetModuleKey(string structName)
        {
            if (structName.StartsWith("ThothSearchOption")) return "thoth_search_option";
            return "root";
        }

        private string GenerateStruct(StructDef structDef, bool inModule)
        {
            var fieldsBuilder = new StringBuilder();
            string indent = inModule ? "    " : "";

            foreach (var prop in structDef.Properties)
            {
                string originalName = prop.Name;
                string snakeCaseName = WzReflectionUtils.ToSnakeCase(originalName);

                // Use the new robust method to get the fully-qualified type name.
                string qualifiedPropType = GetQualifiedTypeName(prop.Type, structDef.Name);

                if (originalName != snakeCaseName && !string.IsNullOrEmpty(originalName))
                {
                    fieldsBuilder.AppendLine($"{indent}    #[serde(rename = \"{originalName}\")]");
                }

                string line = $"{indent}    pub {snakeCaseName}: {qualifiedPropType},";
                fieldsBuilder.AppendLine(line);
            }
            return $"\n#[derive(Serialize, Deserialize, Debug, Default, PartialEq, Clone)]\npub struct {structDef.Name} {{\n{fieldsBuilder.ToString()}{indent}}}";
        }

        /// <summary>
        /// Recursively determines the fully-qualified Rust type name, correctly handling generics.
        /// </summary>
        private string GetQualifiedTypeName(string typeName, string ownerStructName)
        {
            // Regex to capture generic wrappers like Vec<T> or HashMap<K, V>.
            // This regex captures the outer type and the inner content.
            var match = Regex.Match(typeName, @"^(\w+)<(.+)>$");

            if (match.Success)
            {
                string wrapper = match.Groups[1].Value; // e.g., "Vec", "Option", "HashMap"
                string innerContent = match.Groups[2].Value; // e.g., "ThothSearchOptionOption", "i32, Vec<i32>"

                // Split inner content by comma, respecting nested angle brackets
                var innerTypes = SplitTypeArgs(innerContent);
                var qualifiedInnerTypes = innerTypes.Select(t => GetQualifiedTypeName(t.Trim(), ownerStructName));

                return $"{wrapper}<{string.Join(", ", qualifiedInnerTypes)}>";
            }

            if (Primitives.Contains(typeName))
            {
                return typeName; // Primitives don't need qualification.
            }

            string ownerModule = GetModuleKey(ownerStructName);
            string typeModule = GetModuleKey(typeName);

            if (ownerModule != "root" && typeModule == "root")
            {
                return $"crate::{typeName}"; // Reference from a module back to root.
            }
            if (ownerModule == "root" && typeModule != "root")
            {
                return $"{typeModule}::{typeName}"; // Reference from root to a module.
            }

            return typeName; // Type is in the same module or is a primitive.
        }

        private string GenerateEnum(EnumDef enumDef)
        {
            var variants = string.Join(",\n", enumDef.Variants.Select(v => $"    {v}"));
            return $"\n#[derive(Serialize, Deserialize, Debug, PartialEq, Clone)]\npub enum {enumDef.Name} {{\n{variants},\n}}";
        }

        private List<string> SplitTypeArgs(string content)
        {
            var args = new List<string>();
            int bracketLevel = 0;
            int lastSplit = 0;

            for (int i = 0; i < content.Length; i++)
            {
                if (content[i] == '<') bracketLevel++;
                else if (content[i] == '>') bracketLevel--;
                else if (content[i] == ',' && bracketLevel == 0)
                {
                    args.Add(content.Substring(lastSplit, i - lastSplit));
                    lastSplit = i + 1;
                }
            }
            args.Add(content.Substring(lastSplit));
            return args;
        }
    }
}

