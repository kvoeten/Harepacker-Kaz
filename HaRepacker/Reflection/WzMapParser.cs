using MapleLib.WzLib.Serializer.Model;
using MapleLib.WzLib.WzProperties;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;

namespace MapleLib.WzLib.Serializer.Parsers
{
    public class WzMapParser : WzReflectionParser
    {
        public override string TargetFileName => "Map.wz";

        public override bool MatchesFile(string fileName) => fileName == "Map.wz" || fileName == "Map2.wz";

        public override void DefineSchema(ModelBuilder modelBuilder)
        {
            var mapStruct = modelBuilder.GetOrCreateStruct("Map");
            mapStruct.AddProperty(new PropertyDef("_id", "i32", ""));
            mapStruct.AddProperty(new PropertyDef("streetName", "Option<String>", ""));
            mapStruct.AddProperty(new PropertyDef("mapName", "Option<String>", ""));
            mapStruct.AddProperty(new PropertyDef("mapDesc", "Option<String>", ""));
            mapStruct.AddProperty(new PropertyDef("miniMap", "Option<String>", ""));
            mapStruct.AddProperty(new PropertyDef("info", "Info", ""));
            mapStruct.AddProperty(new PropertyDef("life", "Vec<Life>", ""));
            mapStruct.AddProperty(new PropertyDef("reactor", "Vec<Reactor>", ""));
            mapStruct.AddProperty(new PropertyDef("portal", "Vec<Portal>", ""));
            mapStruct.AddProperty(new PropertyDef("ladderRope", "Vec<LadderRope>", ""));
            mapStruct.AddProperty(new PropertyDef("foothold", "Vec<FootholdGroup>", ""));

            // Pre-define structs so they exist in the schema
            modelBuilder.GetOrCreateStruct("Info");
            modelBuilder.GetOrCreateStruct("Life");
            modelBuilder.GetOrCreateStruct("Reactor");
            modelBuilder.GetOrCreateStruct("Portal");
            modelBuilder.GetOrCreateStruct("LadderRope");
            
            var footholdGroup = modelBuilder.GetOrCreateStruct("FootholdGroup");
            footholdGroup.AddProperty(new PropertyDef("layerId", "i32", ""));
            footholdGroup.AddProperty(new PropertyDef("groupId", "i32", ""));
            footholdGroup.AddProperty(new PropertyDef("footholds", "Vec<Foothold>", ""));

            var foothold = modelBuilder.GetOrCreateStruct("Foothold");
            foothold.AddProperty(new PropertyDef("id", "i32", ""));
        }

        public override void ParseAndExportData(WzDirectory wzDir, ModelBuilder modelBuilder, string outputPath)
        {
            var allMaps = new List<Dictionary<string, object>>();
            var mapStruct = modelBuilder.GetOrCreateStruct("Map");
            var infoStruct = modelBuilder.GetOrCreateStruct("Info");
            var lifeStruct = modelBuilder.GetOrCreateStruct("Life");
            var reactorStruct = modelBuilder.GetOrCreateStruct("Reactor");
            var portalStruct = modelBuilder.GetOrCreateStruct("Portal");
            var ladderRopeStruct = modelBuilder.GetOrCreateStruct("LadderRope");
            var footholdGroupStruct = modelBuilder.GetOrCreateStruct("FootholdGroup");
            var footholdStruct = modelBuilder.GetOrCreateStruct("Foothold");

            // Map.wz contains a top-level "Map" directory which contains "MapXX" directories.
            WzDirectory rootMapDir = wzDir.WzDirectories.FirstOrDefault(d => d.Name == "Map");

            // Regex to match "Map" followed by digits (e.g., Map1, Map2, Map001)
            var mapDirRegex = new Regex(@"^Map\d+$", RegexOptions.Compiled);
            
            // If "Map" directory exists, we iterate its children. 
            // Otherwise, we fallback to iterating the root directories (in case of flat structure or error).
            var directoriesToSearch = rootMapDir != null ? rootMapDir.WzDirectories : wzDir.WzDirectories;

            if (rootMapDir == null)
            {
                Debug.WriteLine("Warning: Top-level 'Map' directory not found. searching root.");
            }

            foreach (var mapDir in directoriesToSearch)
            {
                // Debug.WriteLine($"Checking directory: {mapDir.Name}");
                if (!mapDirRegex.IsMatch(mapDir.Name))
                {
                    // Debug.WriteLine($"Skipping directory (regex mismatch): {mapDir.Name}");
                    continue;
                }

                Debug.WriteLine($"Processing Map Directory: {mapDir.Name}");

                foreach (var mapImg in mapDir.WzImages)
                {
                    // Parse ID from image name (e.g., 10520028.img -> 10520028)
                    string imgName = mapImg.Name.Replace(".img", "");
                    if (!int.TryParse(imgName, out int mapId))
                    {
                        // Debug.WriteLine($"Skipping image (not an ID): {mapImg.Name}");
                        continue;
                    }

                    var mapData = new Dictionary<string, object>();
                    mapData["_id"] = mapId;

                    // Add string data
                    var streetName = WzStringCache.Get("map", mapId, "streetName");
                    var mapName = WzStringCache.Get("map", mapId, "mapName");
                    var mapDesc = WzStringCache.Get("map", mapId, "mapDesc");
                    if (streetName != null) mapData["streetName"] = streetName;
                    if (mapName != null) mapData["mapName"] = mapName;
                    if (mapDesc != null) mapData["mapDesc"] = mapDesc;

                    // Extract miniMap canvas image
                    var miniMap = ExtractImageAsBase64(mapImg, "miniMap/canvas");
                    if (miniMap != null) mapData["miniMap"] = miniMap;

                    // --- Parse 'info' ---
                    if (mapImg["info"] is WzSubProperty infoNode)
                    {
                        UpdateSchemaFromNode(infoNode, infoStruct, modelBuilder, $"{mapDir.Name}/{mapImg.Name}/info");
                        mapData["info"] = ParsePropertyNode(infoNode);
                    }

                    // --- Parse 'life' ---
                    if (mapImg["life"] is WzSubProperty lifeNode)
                    {
                        var lifeList = new List<Dictionary<string, object>>();
                        foreach (var lifeItem in lifeNode.WzProperties.OfType<WzSubProperty>())
                        {
                            UpdateSchemaFromNode(lifeItem, lifeStruct, modelBuilder, $"{mapDir.Name}/{mapImg.Name}/life");
                            lifeList.Add(ParsePropertyNode(lifeItem));
                        }
                        mapData["life"] = lifeList;
                    }

                    // --- Parse 'reactor' ---
                    if (mapImg["reactor"] is WzSubProperty reactorNode)
                    {
                        var reactorList = new List<Dictionary<string, object>>();
                        foreach (var reactorItem in reactorNode.WzProperties.OfType<WzSubProperty>())
                        {
                            UpdateSchemaFromNode(reactorItem, reactorStruct, modelBuilder, $"{mapDir.Name}/{mapImg.Name}/reactor");
                            reactorList.Add(ParsePropertyNode(reactorItem));
                        }
                        mapData["reactor"] = reactorList;
                    }

                    // --- Parse 'portal' ---
                    if (mapImg["portal"] is WzSubProperty portalNode)
                    {
                        var portalList = new List<Dictionary<string, object>>();
                        foreach (var portalItem in portalNode.WzProperties.OfType<WzSubProperty>())
                        {
                            UpdateSchemaFromNode(portalItem, portalStruct, modelBuilder, $"{mapDir.Name}/{mapImg.Name}/portal");
                            portalList.Add(ParsePropertyNode(portalItem));
                        }
                        mapData["portal"] = portalList;
                    }

                    // --- Parse 'ladderRope' ---
                    if (mapImg["ladderRope"] is WzSubProperty ladderRopeNode)
                    {
                        var ladderRopeList = new List<Dictionary<string, object>>();
                        foreach (var ladderRopeItem in ladderRopeNode.WzProperties.OfType<WzSubProperty>())
                        {
                            UpdateSchemaFromNode(ladderRopeItem, ladderRopeStruct, modelBuilder, $"{mapDir.Name}/{mapImg.Name}/ladderRope");
                            ladderRopeList.Add(ParsePropertyNode(ladderRopeItem));
                        }
                        mapData["ladderRope"] = ladderRopeList;
                    }

                    // --- Parse 'foothold' ---
                    if (mapImg["foothold"] is WzSubProperty footholdNode)
                    {
                        var footholdGroups = new List<Dictionary<string, object>>();
                        
                        // Traverse: foothold -> layer -> group -> foothold
                        foreach (var layer in footholdNode.WzProperties.OfType<WzSubProperty>())
                        {
                            if (!int.TryParse(layer.Name, out int layerId)) continue;

                            foreach (var group in layer.WzProperties.OfType<WzSubProperty>())
                            {
                                if (!int.TryParse(group.Name, out int groupId)) continue;

                                var groupData = new Dictionary<string, object>();
                                groupData["layerId"] = layerId;
                                groupData["groupId"] = groupId;

                                var footholdsInGroup = new List<Dictionary<string, object>>();

                                // Collect and sort footholds by ID
                                var sortedFootholds = group.WzProperties.OfType<WzSubProperty>()
                                    .Select(fh => new { Node = fh, Id = int.TryParse(fh.Name, out int id) ? id : -1 })
                                    .Where(x => x.Id != -1)
                                    .OrderBy(x => x.Id)
                                    .ToList();

                                foreach (var item in sortedFootholds)
                                {
                                    UpdateSchemaFromNode(item.Node, footholdStruct, modelBuilder, $"{mapDir.Name}/{mapImg.Name}/foothold");
                                    var fhData = ParsePropertyNode(item.Node);
                                    fhData["id"] = item.Id;
                                    footholdsInGroup.Add(fhData);
                                }

                                groupData["footholds"] = footholdsInGroup;
                                footholdGroups.Add(groupData);
                            }
                        }
                        mapData["foothold"] = footholdGroups;
                    }

                    allMaps.Add(mapData);
                }
            }

            Debug.WriteLine($"Total maps processed: {allMaps.Count}");
            ExportDataToFile(allMaps, "maps", outputPath);
        }
    }
}
