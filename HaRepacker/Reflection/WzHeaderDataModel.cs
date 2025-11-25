using Newtonsoft.Json;
using System.Collections.Generic;

namespace MapleLib.WzLib.Serializer.DataModels
{
    // C# classes mirroring the schema, used for serializing data to BSON/JSON.

    #region Item Data Models
    public class ItemData
    {
        [JsonProperty("_id")]
        public int Id { get; set; }
        public string ItemType { get; set; }
        public string EquipType { get; set; }
        public string Name { get; set; }
        public string Desc { get; set; }
        public string Icon { get; set; }
        public string Sample { get; set; }
        public Dictionary<string, object> Info { get; set; } = new();
        public Dictionary<string, object> Spec { get; set; } = new();
    }
    #endregion

    #region ThothSearchOption Data Models
    public class ThothSearchOptionData
    {
        [JsonProperty("hot")]
        public List<ThothSearchOptionOptionData> Hot { get; set; } = new();
        [JsonProperty("regular")]
        public List<ThothSearchOptionOptionData> Regular { get; set; } = new();
        [JsonProperty("item_category")]
        public List<ThothSearchOptionItemCategoryData> ItemCategory { get; set; } = new();
        [JsonProperty("default_settings")]
        public List<ThothSearchOptionDefaultSettingData> DefaultSettings { get; set; } = new();
    }

    public class ThothSearchOptionOptionData
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("string")]
        public string DisplayString { get; set; }
        [JsonProperty("index")]
        public short Index { get; set; }
    }
    
    public class ThothSearchOptionItemCategoryData
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("item_detail_category")]
        public List<ThothSearchOptionItemDetailCategoryData> ItemDetailCategory { get; set; } = new();
    }

    public class ThothSearchOptionItemDetailCategoryData
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("string")]
        public string DisplayString { get; set; }
        [JsonProperty("begin")]
        public int Begin { get; set; }
        [JsonProperty("end")]
        public int End { get; set; }
    }

    public class ThothSearchOptionDefaultSettingData
    {
        [JsonProperty("job_name")]
        public string JobName { get; set; }
        [JsonProperty("weapon")]
        public string Weapon { get; set; }
        [JsonProperty("option")]
        public string Option { get; set; }
        [JsonProperty("job")]
        public int Job { get; set; }
    }
    #endregion
}
