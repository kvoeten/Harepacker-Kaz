using MapleLib.WzLib.Serializer.CodeGenerators;
using MapleLib.WzLib.Serializer.Model;
using MapleLib.WzLib.Serializer.Parsers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace MapleLib.WzLib.Serializer
{
    /// <summary>
    /// The main dispatcher class that orchestrates the serialization process.
    /// It uses a list of file parsers and a code generator to process WZ files.
    /// </summary>
    public class WzReflectionSerializer : WzSerializer, IWzImageSerializer, IWzFileSerializer, IWzDirectorySerializer
    {
        private readonly List<IWzReflectionFileParser> _fileParsers;
        private readonly IWzReflectionCodeGenerator _codeGenerator;

        // Default constructor uses Rust, but could be configured for other languages.
        // TODO: Allow generator selection :) (Maybe external format declaration?)
        public WzReflectionSerializer() : this(0, LineBreak.Windows, new WzCodeGeneratorRust()) { }

        public WzReflectionSerializer(int indentation, LineBreak lineBreakType) : this(indentation, lineBreakType, new WzCodeGeneratorRust()) { }

        public WzReflectionSerializer(int indentation, LineBreak lineBreakType, IWzReflectionCodeGenerator codeGenerator)
             : base(indentation, lineBreakType)
        {
            _codeGenerator = codeGenerator;
            _fileParsers = new List<IWzReflectionFileParser>
            {
                new WzItemParser(),
                // TODO: Mob, Npc, Map, Etc, Etc.
            };
        }

        void IWzImageSerializer.SerializeImage(WzImage img, string path) => ProcessDirectory(img.WzFileParent.WzDirectory, path);
        void IWzFileSerializer.SerializeFile(WzFile file, string path) => ProcessDirectory(file.WzDirectory, path);
        void IWzDirectorySerializer.SerializeDirectory(WzDirectory dir, string path) => ProcessDirectory(dir, path);

        /// <summary>
        /// Processes a WZ directory by finding the correct parser and orchestrating the process.
        /// </summary>
        private void ProcessDirectory(WzDirectory wzDir, string outputPath)
        {
            var parser = _fileParsers.FirstOrDefault(p => p.TargetFileName == wzDir.Name);
            if (parser == null) return; // No parser for this file, skip.

            var modelBuilder = new ModelBuilder();

            // Let the specific parser define its language-agnostic schema.
            parser.DefineSchema(modelBuilder);

            // Let the specific parser extract and write its data to BSON/JSON.
            parser.ParseAndExportData(wzDir, modelBuilder, outputPath);

            // --- Enhanced Logging ---
#if DEBUG
            LogModelBuilderState(modelBuilder);
#endif

            // Generate the final, language-specific schema file.
            string code = _codeGenerator.GenerateCode(modelBuilder);
            string filePath = Path.ChangeExtension(outputPath, _codeGenerator.FileExtension);
            File.WriteAllText(filePath, code);
        }

#if DEBUG
        private void LogModelBuilderState(ModelBuilder modelBuilder)
        {
            var log = new StringBuilder();
            log.AppendLine("--- ModelBuilder State Before Code Generation ---");

            log.AppendLine("\n[ENUMS]");
            foreach (var enumDef in modelBuilder.Enums.OrderBy(e => e.Name))
            {
                log.AppendLine($"  - {enumDef.Name}:");
                if (enumDef.Variants.Any())
                {
                    foreach (var variant in enumDef.Variants)
                    {
                        log.AppendLine($"    - {variant}");
                    }
                }
                else
                {
                    log.AppendLine("    (No variants discovered)");
                }
            }

            log.AppendLine("\n[STRUCTS]");
            foreach (var structDef in modelBuilder.Structs.OrderBy(s => s.Name))
            {
                log.AppendLine($"  - {structDef.Name}:");
                if (structDef.Properties.Any())
                {
                    foreach (var prop in structDef.Properties)
                    {
                        log.AppendLine($"    - {prop.Name}: {prop.Type}");
                    }
                }
                else
                {
                    log.AppendLine("    (No properties discovered)");
                }
            }

            Debug.WriteLine(log.ToString());
        }
#endif
    }
}
