using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Dynamic;

namespace MapleLib.WzLib.Serializer.Model
{
    /// <summary>
    /// The central class for defining and accessing models. It acts as a repository for
    /// both model definitions (schemas) and the data instances created from them.
    /// </summary>
    public class HeaderModelBuilder
    {
        private readonly Dictionary<string, HeaderModelDefinition> _models = new Dictionary<string, HeaderModelDefinition>();
        private readonly Dictionary<string, List<DataObject>> _data = new Dictionary<string, List<DataObject>>();

        /// <summary>
        /// Retrieves a model definition by name.
        /// </summary>
        public HeaderModelDefinition Get(string name) => _models.TryGetValue(name, out var model) ? model : null;

        /// <summary>
        /// Defines a new model schema using a configuration action.
        /// </summary>
        public void Define(string name, Action<HeaderModelDefinition> definitionAction)
        {
            var model = new HeaderModelDefinition(name, this);
            _models[name] = model;
            definitionAction(model);
        }

        /// <summary>
        /// Adds a created data instance to the central data repository, organized by model name.
        /// </summary>
        public void AddData(string modelName, DataObject dataObject)
        {
            if (!_data.ContainsKey(modelName))
            {
                _data[modelName] = new List<DataObject>();
            }
            _data[modelName].Add(dataObject);
        }
    }

    /// <summary>
    /// Defines the schema of a data object, including its properties and relationships.
    /// </summary>
    public class HeaderModelDefinition
    {
        public string Name { get; }
        private readonly HeaderModelBuilder _builder;

        public HeaderModelDefinition(string name, HeaderModelBuilder builder)
        {
            Name = name;
            _builder = builder;
        }

        /// <summary>
        /// Creates a new instance of this model.
        /// </summary>
        public DataObject Create() => new DataObject(this);

        public void HasString(string name) { }
        public void HasShort(string name) { }
        public void HasInt(string name) { }
        public void HasLong(string name) { }
        public void HasFloat(string name) { }
        public void HasDouble(string name) { }
        public void HasVector(string name) { }
        public void HasObject(string name) { }

        public void HasOne(string modelName, string propertyName) { }
        public void HasMany(string itemModelName, string collectionName, string keyProperty) { }
    }

    /// <summary>
    /// A dynamic data object that holds properties. It uses a custom JsonConverter
    /// to ensure clean serialization.
    /// </summary>
    [JsonConverter(typeof(DataObjectConverter))]
    public class DataObject
    {
        private readonly Dictionary<string, object> _properties = new Dictionary<string, object>();

        public DataObject(HeaderModelDefinition model) { }

        public object this[string key]
        {
            get => _properties.TryGetValue(key, out var value) ? value : null;
            set => _properties[key] = value;
        }

        public void Add(string collectionName, DataObject item)
        {
            if (!_properties.TryGetValue(collectionName, out var collection))
            {
                collection = new List<DataObject>();
                _properties[collectionName] = collection;
            }
            (collection as List<DataObject>)?.Add(item);
        }

        internal Dictionary<string, object> GetBackingDictionary() => _properties;
    }

    /// <summary>
    /// Custom JsonConverter to serialize only the internal dictionary of a DataObject,
    /// resulting in clean JSON output.
    /// </summary>
    public class DataObjectConverter : JsonConverter<DataObject>
    {
        public override void WriteJson(JsonWriter writer, DataObject value, JsonSerializer serializer)
        {
            serializer.Serialize(writer, value.GetBackingDictionary());
        }

        public override DataObject ReadJson(JsonReader reader, Type objectType, DataObject existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException("Deserialization is not required for this tool.");
        }
    }
}