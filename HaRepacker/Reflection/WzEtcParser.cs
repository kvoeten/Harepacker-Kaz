using MapleLib.WzLib;
using MapleLib.WzLib.Serializer.Model;
using MapleLib.WzLib.WzProperties;
using MapleLib.WzLib.WzStructure;
using Microsoft.VisualBasic.Logging;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MapleLib.WzLib.Serializer.Parsers
{
    /// <summary>
    /// A dynamic parser for Etc.wz. It distinguishes between top-level directories (categories)
    /// and top-level images (standalone entries) to build an accurate data schema.
    /// </summary>
    public class WzEtcParser : WzReflectionParser
    {
        public override string TargetFileName => "Etc.wz";

        public override void DefineSchema(ModelBuilder modelBuilder)
        {
            // Schema is generated dynamically during the data parsing phase.
            var genderData = modelBuilder.GetOrCreateStruct("GenderData");
            genderData.AddProperty(new PropertyDef("maleData", "HashMap<i32, Vec<i32>>", ""));
            genderData.AddProperty(new PropertyDef("femaleData", "HashMap<i32, Vec<i32>>", ""));
        }

        public override void ParseAndExportData(WzDirectory wzDir, ModelBuilder modelBuilder, string outputPath)
        {
            /*
             *  Etc.wz is very chaotic with many unique data entires. Sanitizing the export requires a more manual approach.
             *  Property discover is limited, unhandled elements are to be logged so new manual entries can be made.
             */

            string[] unhandled = { };

            // Process top-level directories
            foreach (var categoryDir in wzDir.WzDirectories)
            {
                switch (categoryDir.Name)
                {
                    default:
                        unhandled.Append(categoryDir.Name);
                        break;
                }
            }

            // Process top-level images
            foreach (var image in wzDir.WzImages)
            {
               
               switch(image.Name)
               {
                    case "MakeCharInfo.img":
                        ProcessMakeCharInfo(image, modelBuilder, outputPath);
                        break;
                    default:
                        unhandled.Append(image.Name);
                        break;
               }
                 
            }

            foreach (string failed in unhandled)
            {
                Debug.WriteLine("Unhandled Etc.wz Entry!! [ " + failed + "]");
            }

        }

        public struct GenderData
        {
            // List of valid item id's per bodypart
            public Dictionary<int, List<int>> maleData;
            public Dictionary<int, List<int>> femaleData;
        }

        /// <summary>
        /// Processes a single .img file at the root as a standalone data structure.
        /// </summary>
        private void ProcessMakeCharInfo(WzImage image, ModelBuilder modelBuilder, string outputPath)
        {
            string structName = WzReflectionUtils.ToPascalCase(WzReflectionUtils.SanitizeName(image.Name));
            var rootStruct = modelBuilder.GetOrCreateStruct(structName);

            Dictionary<int, GenderData> JobGenderData = new Dictionary<int, GenderData>();

            foreach (WzSubProperty job in image.WzProperties)
            {
                ProcessJobMakeCharInfo(JobGenderData, job, modelBuilder, outputPath);
            }

            /* Output should be bson/ json for "MakeCharInfo" with the Dictionary<int, GenderData> data,
            * and a header definition for Dictionary<int, GenderData> named 
            * MakeCharInfo (in correct naming scheme for export language, e.g make_char_info for rust) */
            
            /* Output should be bson/ json for "MakeCharInfo" with the Dictionary<int, GenderData> data,
            * and a header definition for Dictionary<int, GenderData> named 
            * MakeCharInfo (in correct naming scheme for export language, e.g make_char_info for rust) */
            
            ExportDataToFile(JobGenderData, "make_char_info", outputPath);
        }

        private void ProcessJobMakeCharInfo(Dictionary<int, GenderData> JobGenderData, WzSubProperty job, ModelBuilder modelBuilder, string outputPath)
        {
            Debug.WriteLine($"Processing Job: {job.Name}");
            int jobId = -1;

            // Helper to find property case-insensitively or by specific known names
            WzSubProperty FindSubProperty(WzSubProperty parent, string name)
            {
                return (WzSubProperty)parent.WzProperties.Find(obj => obj.Name.Equals(name, System.StringComparison.OrdinalIgnoreCase));
            }

            WzSubProperty maleData = FindSubProperty(job, "male");
            if (maleData == null) maleData = FindSubProperty(job, "Male"); // Explicit check if case-insensitive didn't work or if we want to be sure
            
            WzSubProperty femaleData = FindSubProperty(job, "female");
            if (femaleData == null) femaleData = FindSubProperty(job, "Female");

            // Get job ID and adjust male/female data if needed..
            if (!int.TryParse(job.Name, out jobId))
            {
                switch (job.Name)
                {
                    case "CharFemale":
                        jobId = 0;
                        femaleData = job;
                        break;
                    case "CharMale":
                        jobId = 0;
                        maleData = job;
                        break;
                    case "Info":
                        jobId = 0;
                        maleData = FindSubProperty(job, "CharMale");
                        femaleData = FindSubProperty(job, "CharFemale");
                        break;
                    case "EvanCharFemale":
                        jobId = 30000;
                        femaleData = job;
                        break;
                    case "EvanCharMale":
                        jobId = 30000;
                        maleData = job;
                        break;
                    case "JumpingCharacter":
                        jobId = 0431;
                        var subProp = (WzSubProperty)job.WzProperties.Find(obj => obj.Name == "0431");
                        if (subProp != null)
                        {
                            maleData = FindSubProperty(subProp, "Male");
                            femaleData = FindSubProperty(subProp, "Female");
                        }
                        break;
                    case "OrientCharFemale":
                        jobId = 0;
                        femaleData = job;
                        break;
                    case "OrientCharMale":
                        jobId = 0;
                        maleData = job;
                        break;
                    case "PremiumCharFemale":
                        jobId = 0;
                        femaleData = job;
                        break;
                    case "PremiumCharMale":
                        jobId = 0;
                        maleData = job;
                        break;
                    case "ResistanceCharFemale":
                        jobId = 0;
                        femaleData = job;
                        break;
                    case "ResistanceCharMale":
                        jobId = 0;
                        maleData = job;
                        break;
                    case "UltimateAdventurer":
                        foreach (WzSubProperty ultimateJob in job.WzProperties)
                        {
                            ProcessJobMakeCharInfo(JobGenderData, ultimateJob, modelBuilder, outputPath);
                        }
                        return; // Return after processing children
                    case "3001_Dummy":
                        jobId = 3001;
                        break;
                    case "10112_Dummy":
                        jobId = 10112;
                        break;
                    default:
                        Debug.WriteLine("Failed to parse MakeCharInfo for: " + job.Name);
                        return;
                }
            }

            // Now add the data
            if (maleData == null && femaleData == null)
            {
                Debug.WriteLine("Failed to parse MakeCharInfo (No Gender Data) for: " + job.Name);
                return;
            }
            else
            {
                // Duplicate entries for non-binary genders
                if (maleData == null) maleData = femaleData;
                else if (femaleData == null) femaleData = maleData;
            }

            // For each bodypart a list of valid ItemID's
            GenderData BPOptions;
            if (!JobGenderData.TryGetValue(jobId, out BPOptions))
            {
                BPOptions = new GenderData
                {
                    maleData = new Dictionary<int, List<int>>(),
                    femaleData = new Dictionary<int, List<int>>()
                };
            }

            void ProcessGenderData(WzSubProperty data, Dictionary<int, List<int>> targetDict)
            {
                if (data == null) return;
                foreach (WzSubProperty bodyPart in data.WzProperties)
                {
                    if (!int.TryParse(bodyPart.Name, out int bodyPartId))
                        continue;

                    foreach (var prop in bodyPart.WzProperties)
                    {
                        if (prop is WzIntProperty itemId)
                        {
                            if (!targetDict.TryGetValue(bodyPartId, out List<int> BodyPartItems))
                            {
                                BodyPartItems = new List<int>();
                                targetDict[bodyPartId] = BodyPartItems;
                            }
                            BodyPartItems.Add(itemId.GetInt());
                        }
                        else
                        {
                            string fullPath = $"Etc.wz/MakeCharInfo.img/{job.Name}/{data.Name}/{bodyPart.Name}/{prop.Name}";
                            Debug.WriteLine($"[WARNING] Non-integer property found: {fullPath} (Type: {prop.GetType().Name})");
                        }
                    }
                }
            }

            ProcessGenderData(maleData, BPOptions.maleData);
            ProcessGenderData(femaleData, BPOptions.femaleData);

            // Push back into map (update or add)
            JobGenderData[jobId] = BPOptions;
            Debug.WriteLine($"Processed Job {jobId} successfully.");
        }
    }
}

