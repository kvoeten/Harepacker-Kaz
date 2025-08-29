using System.Collections.Generic;
using System.Linq;

namespace MapleLib.WzLib.Serializer.Model
{
    // Language-agnostic records for defining a schema
    public record PropertyDef(string Name, string Type, string SourcePath);

    public class StructDef
    {
        public string Name { get; }
        private readonly Dictionary<string, PropertyDef> _properties = new();
        public IEnumerable<PropertyDef> Properties => _properties.Values.OrderBy(p => p.Name);
        public StructDef(string name) { Name = name; }
        public void AddProperty(PropertyDef prop) => _properties[prop.Name] = prop;
    }

    public class EnumDef
    {
        public string Name { get; }
        private readonly HashSet<string> _variants = new();
        public IEnumerable<string> Variants => _variants.OrderBy(v => v);
        public EnumDef(string name) { Name = name; }
        public void AddVariant(string name) => _variants.Add(name);
    }

    /// <summary>
    /// A builder that aggregates schema definitions (structs and enums) from multiple parsers.
    /// </summary>
    public class ModelBuilder
    {
        private readonly Dictionary<string, StructDef> _structs = new();
        private readonly Dictionary<string, EnumDef> _enums = new();

        public ICollection<StructDef> Structs => _structs.Values;
        public ICollection<EnumDef> Enums => _enums.Values;

        public StructDef GetOrCreateStruct(string name)
        {
            if (!_structs.TryGetValue(name, out var s))
            {
                s = new StructDef(name);
                _structs[name] = s;
            }
            return s;
        }

        public EnumDef GetOrCreateEnum(string name)
        {
            if (!_enums.TryGetValue(name, out var e))
            {
                e = new EnumDef(name);
                _enums[name] = e;
            }
            return e;
        }
    }
}
