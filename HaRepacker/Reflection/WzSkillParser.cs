using MapleLib.WzLib.Serializer.Model;
using MapleLib.WzLib.WzProperties;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace MapleLib.WzLib.Serializer.Parsers
{
    public class WzSkillParser : WzReflectionParser
    {
        public override string TargetFileName => "Skill.wz";

        public override void DefineSchema(ModelBuilder modelBuilder)
        {
            var skillStruct = modelBuilder.GetOrCreateStruct("Skill");
            skillStruct.AddProperty(new PropertyDef("_id", "i32", ""));
            skillStruct.AddProperty(new PropertyDef("levels", "Vec<SkillLevel>", ""));

            var skillLevelStruct = modelBuilder.GetOrCreateStruct("SkillLevel");
            skillLevelStruct.AddProperty(new PropertyDef("level", "i32", ""));
        }

        public override void ParseAndExportData(WzDirectory wzDir, ModelBuilder modelBuilder, string outputPath)
        {
            var allSkills = new List<Dictionary<string, object>>();
            var skillStruct = modelBuilder.GetOrCreateStruct("Skill");
            var skillLevelStruct = modelBuilder.GetOrCreateStruct("SkillLevel");

            foreach (var skillImg in wzDir.WzImages)
            {
                // Only process numeric image names (skip string-named ones for now)
                string imgName = skillImg.Name.Replace(".img", "");
                if (!int.TryParse(imgName, out int _))
                {
                    // Debug.WriteLine($"Skipping non-numeric image: {skillImg.Name}");
                    continue;
                }

                // Access the "skill" sub-property
                if (!(skillImg["skill"] is WzSubProperty skillContainer))
                {
                    // Debug.WriteLine($"Skipping {skillImg.Name} (no 'skill' node)");
                    continue;
                }

                // Iterate through each skill under the "skill" container
                foreach (var skillNode in skillContainer.WzProperties.OfType<WzSubProperty>())
                {
                    if (!int.TryParse(skillNode.Name, out int skillId))
                    {
                        // Debug.WriteLine($"Skipping non-numeric skill: {skillNode.Name}");
                        continue;
                    }

                    var skillData = new Dictionary<string, object>();
                    skillData["_id"] = skillId;

                    var levels = new List<Dictionary<string, object>>();

                    // Iterate through skill properties
                    foreach (var prop in skillNode.WzProperties)
                    {
                        if (prop.Name == "level" && prop is WzSubProperty levelContainer)
                        {
                            // Parse level data
                            foreach (var levelNode in levelContainer.WzProperties.OfType<WzSubProperty>())
                            {
                                if (!int.TryParse(levelNode.Name, out int levelNum))
                                    continue;

                                // Update schema for level properties
                                UpdateSchemaFromNode(levelNode, skillLevelStruct, modelBuilder, $"{skillImg.Name}/skill/{skillNode.Name}/level/{levelNode.Name}");

                                var levelData = ParsePropertyNode(levelNode);
                                levelData["level"] = levelNum;
                                levels.Add(levelData);
                            }
                        }
                        else if (prop is WzSubProperty subProp)
                        {
                            // Update schema for skill properties (excluding level)
                            UpdateSchemaFromNode(subProp, skillStruct, modelBuilder, $"{skillImg.Name}/skill/{skillNode.Name}/{prop.Name}");
                            skillData[prop.Name] = ParsePropertyNode(subProp);
                        }
                        else
                        {
                            // Add simple properties directly
                            var value = prop switch
                            {
                                WzStringProperty str => (object)str.Value,
                                WzShortProperty s => (object)s.Value,
                                WzIntProperty i => (object)i.Value,
                                WzLongProperty l => (object)l.Value,
                                WzFloatProperty f => (object)f.Value,
                                WzDoubleProperty d => (object)d.Value,
                                _ => null
                            };
                            if (value != null)
                            {
                                skillData[prop.Name] = value;
                            }
                        }
                    }

                    skillData["levels"] = levels;
                    allSkills.Add(skillData);
                }
            }

            Debug.WriteLine($"Total skills processed: {allSkills.Count}");
            ExportDataToFile(allSkills, "skills", outputPath);
        }
    }
}
