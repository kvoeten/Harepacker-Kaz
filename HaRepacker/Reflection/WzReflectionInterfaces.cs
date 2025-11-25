using MapleLib.WzLib.Serializer.Model;

namespace MapleLib.WzLib.Serializer
{
    /// <summary>
    /// Defines a contract for a class that can parse a specific WZ file.
    /// </summary>
    public interface IWzReflectionFileParser
    {
        /// <summary>
        /// The name of the WZ file this parser is responsible for (e.g., "Item.wz").
        /// </summary>
        string TargetFileName { get; }

        /// <summary>
        /// Checks if this parser can handle the given WZ file name.
        /// </summary>
        /// <param name="fileName">The WZ file name to check.</param>
        /// <returns>True if this parser can handle the file, false otherwise.</returns>
        bool MatchesFile(string fileName);

        /// <summary>
        /// Defines the language-agnostic schema (structs, enums) for this file's data.
        /// </summary>
        /// <param name="modelBuilder">The model builder to define the schema in.</param>
        void DefineSchema(ModelBuilder modelBuilder);

        /// <summary>
        /// Parses the WZ file, extracts all relevant data, and exports it to BSON/JSON files.
        /// </summary>
        /// <param name="wzDir">The root directory of the WZ file.</param>
        /// <param name="outputPath">The base path for output files.</param>
        void ParseAndExportData(WzDirectory wzDir, ModelBuilder modelBuilder, string outputPath);
    }

    /// <summary>
    /// Defines a contract for a class that can generate a language-specific code file from a schema.
    /// </summary>
    public interface IWzReflectionCodeGenerator
    {
        /// <summary>
        /// The file extension for the generated code file (e.g., ".rs", ".hpp").
        /// </summary>
        string FileExtension { get; }

        /// <summary>
        /// Generates the code as a single string from the completed model builder.
        /// </summary>
        /// <param name="modelBuilder">The model builder containing the complete schema.</param>
        /// <returns>A string containing the source code for the schema file.</returns>
        string GenerateCode(ModelBuilder modelBuilder);
    }
}
