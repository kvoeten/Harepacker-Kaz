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

        public WzReflectionSerializer() : this(0, LineBreak.Windows, new WzCodeGeneratorRust()) { }

        public WzReflectionSerializer(int indentation, LineBreak lineBreakType) : this(indentation, lineBreakType, new WzCodeGeneratorRust()) { }

        public WzReflectionSerializer(int indentation, LineBreak lineBreakType, IWzReflectionCodeGenerator codeGenerator)
             : base(indentation, lineBreakType)
        {
            _codeGenerator = codeGenerator;
            _fileParsers = new List<IWzReflectionFileParser>
            {
                new WzStringParser(), // Must be first to populate string cache
                new WzItemParser(),
                new WzEtcParser(),
                new WzCharacterParser(),
                new WzMapParser(),
                new WzMobParser(),
                new WzNpcParser(),
                new WzReactorParser(),
                new WzSkillParser(),
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
            // Auto-discover and load String.wz if it exists in the same directory
            AutoLoadStringData(wzDir);

            var parser = _fileParsers.FirstOrDefault(p => p.MatchesFile(wzDir.Name));
            if (parser == null) return; // No parser for this file, skip.

            var modelBuilder = new ModelBuilder();

            // The parser defines its schema (or discovers it) and parses the data.
            parser.DefineSchema(modelBuilder);
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

        /// <summary>
        /// Automatically discovers and loads String.wz from the same directory as the current WZ file.
        /// This ensures string data is available even if the user didn't explicitly select String.wz.
        /// </summary>
        private void AutoLoadStringData(WzDirectory wzDir)
        {
            // Skip if this IS String.wz (avoid recursion)
            if (wzDir.Name == "String.wz")
                return;

            // Check if strings are already loaded
            if (WzStringCache.Get("map", 100000000, "mapName") != null)
            {
                Debug.WriteLine("String cache already populated, skipping auto-load.");
                return;
            }

            // Try to find String.wz in the same directory
            string wzFilePath = wzDir.WzFileParent?.FilePath;
            if (string.IsNullOrEmpty(wzFilePath))
                return;

            string wzDirectory = Path.GetDirectoryName(wzFilePath);
            if (string.IsNullOrEmpty(wzDirectory))
                return;

            string stringWzPath = Path.Combine(wzDirectory, "String.wz");
            if (!File.Exists(stringWzPath))
            {
                Debug.WriteLine($"String.wz not found at {stringWzPath}, string enrichment unavailable.");
                return;
            }

            try
            {
                Debug.WriteLine($"Auto-loading String.wz from {stringWzPath}...");
                
                // Load String.wz
                var stringWzFile = new WzFile(stringWzPath, wzDir.WzFileParent.MapleVersion);
                stringWzFile.ParseWzFile();

                // Process with WzStringParser
                var stringParser = new WzStringParser();
                var tempModelBuilder = new ModelBuilder();
                stringParser.DefineSchema(tempModelBuilder);
                stringParser.ParseAndExportData(stringWzFile.WzDirectory, tempModelBuilder, wzDirectory);

                Debug.WriteLine("String.wz auto-loaded successfully.");
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"Failed to auto-load String.wz: {ex.Message}");
            }
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
                        log.AppendLine($"    - {prop.Name}: {prop.Type} (Source: {prop.SourcePath})");
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
