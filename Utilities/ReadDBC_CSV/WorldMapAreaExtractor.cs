﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharedLib;

namespace ReadDBC_CSV
{
    public class WorldMapAreaExtractor : IExtractor
    {
        private readonly string path;
        private readonly string wma_build;

        public List<string> FileRequirement { get; } = new List<string>()
        {
            "worldmaparea.csv",
            "uimap.csv"
        };

        private List<string> acceptedOverride = new List<string> { "Hellfire", "Kalimdor" };

        private const string expansion01Continent = "Expansion01";

        private readonly Dictionary<int, string> tbc_expansionContinent = new Dictionary<int, string>
        {
            { 464, expansion01Continent }, // AzuremystIsle
            { 476, expansion01Continent }, // BloodmystIsle
            { 471, expansion01Continent }, // TheExodar
                   
            { 462, expansion01Continent }, // EversongWoods
            { 463, expansion01Continent }, // Ghostlands
            { 480, expansion01Continent }, // SilvermoonCity
                   
            { 499, expansion01Continent }, // Sunwell
        };



        public WorldMapAreaExtractor(string path, string wma_build)
        {
            this.path = path;
            this.wma_build = wma_build;
        }

        public void Run()
        {
            Func<string[], WorldMapArea> wmaParser = CreateV2;

            if (Version.TryParse(wma_build, out Version version) && version.Major == 1)
                wmaParser = CreateV1;

            var list = File.ReadAllLines(Path.Join(path, FileRequirement[0])).ToList().
                Skip(1).Select(l => l.Split(",")).Select(l => wmaParser(l)).ToList();

            CorrectTypos(ref list);

            var uimapLines = File.ReadAllLines(Path.Join(path, FileRequirement[1])).ToList().
                Select(l => l.Split(","));
            list.ForEach(wmp => PopulateUIMap(wmp, uimapLines));

            list.ForEach(l =>
            {
                if (tbc_expansionContinent.ContainsKey(l.ID))
                {
                    l.Continent = tbc_expansionContinent[l.ID];
                    Console.WriteLine($" - {l.AreaName} expansion continent set to {l.Continent}");
                }
            });

            Console.WriteLine("Unsupported mini maps areas: " + string.Join(", ", list.Where(l => l.UIMapId == 0).Select(s => s.AreaName).OrderBy(s => s)));

            var duplicates = list.GroupBy(s => s.MapID).Where(g => g.Count() > 1).Select(g => g.Key);
            Console.WriteLine("Duplicated 'MapID' (continents accepted): " + string.Join(", ", duplicates.ToArray()));

            File.WriteAllText(Path.Join(path, "WorldMapArea.json"), JsonConvert.SerializeObject(list, Formatting.Indented));
        }

        // https://wow.tools/dbc/?dbc=worldmaparea&build=2.4.3.8606
        private WorldMapArea CreateV2(string[] values)
        {
            return new WorldMapArea
            {
                ID = int.Parse(values[0]),
                MapID = int.Parse(values[1]),
                AreaID = int.Parse(values[2]),
                AreaName = values[3],
                LocLeft = float.Parse(values[4]),
                LocRight = float.Parse(values[5]),
                LocTop = float.Parse(values[6]),
                LocBottom = float.Parse(values[7]),
            };
        }

        // https://wow.tools/dbc/?dbc=worldmaparea&build=1.13.0.28377
        private WorldMapArea CreateV1(string[] values)
        {
            return new WorldMapArea
            {
                AreaName = values[0],
                LocLeft = float.Parse(values[1]),
                LocRight = float.Parse(values[2]),
                LocTop = float.Parse(values[3]),
                LocBottom = float.Parse(values[4]),

                MapID = int.Parse(values[6]),
                AreaID = int.Parse(values[7]),

                ID = int.Parse(values[15]),
            };
        }

        private void PopulateUIMap(WorldMapArea area, IEnumerable<string[]> uimapLines)
        {
            var kalimdor = uimapLines.Where(s => s[0] == "Kalimdor").Select(s => s[1]).FirstOrDefault();

            // two outland occurrences need the last one
            var outland = uimapLines.Where(s => s[0] == "Outland").Select(s => s[1]).LastOrDefault();

            var matches = uimapLines.Where(s => Matches(area, s))
                .ToList();

            if (matches.Count > 1)
            {
                Console.WriteLine($"\n- WARN [{area.AreaName}] has more than one matches:\n {string.Join(",\n ", matches.Select(t => new { AreaName = t[0], UIMapId = t[1] }))}");
            }

            if (matches.Count == 0)
            {
                Console.WriteLine($"\n- WARN [{area.AreaName}] has no matches!");
            }

            matches.ForEach(a =>
            {
                if (area.UIMapId == 0 || acceptedOverride.Contains(area.AreaName))
                {
                    if(area.UIMapId != 0 && acceptedOverride.Contains(area.AreaName))
                    {
                        Console.WriteLine($" - Accepted override [{area.AreaName}] from [{area.UIMapId}] to [{int.Parse(a[1])}]");
                    }

                    area.UIMapId = int.Parse(a[1]);
                }
                else
                {
                    Console.WriteLine($" - Prevented override [{area.AreaName}] from [{area.UIMapId}] to [{int.Parse(a[1])}]");
                }

                area.Continent = a[2] == outland ? expansion01Continent : (a[2] == kalimdor ? "Kalimdor" : "Azeroth");
            });
        }

        /// <summary>
        /// Fix occurance where Stormwind and Stormwind city didn't match
        /// </summary>
        /// <param name="area"></param>
        /// <param name="s"></param>
        /// <returns></returns>
        private bool Matches(WorldMapArea area, string[] s)
        {
            var areaname = s[0].Replace(" ", "").Replace("'", "");
            var areaname1 = area.AreaName.Replace(" ", "").Replace("'", "");
            return areaname.StartsWith(areaname1, StringComparison.InvariantCultureIgnoreCase)
                 || areaname1.StartsWith(areaname, StringComparison.InvariantCultureIgnoreCase);
        }

        private void CorrectTypos(ref List<WorldMapArea> list)
        {
            // Unsupported mini maps areas: Aszhara, Barrens, Darnassis, Expansion01, Hilsbrad, Hinterlands, Ogrimmar, Sunwell
            // Unsupported mini maps areas: Expansion01, Sunwell
            for (int i =0; i<list.Count; i++)
            {
                // typo :dense:
                if (list[i].AreaName == "Aszhara")
                    list[i].AreaName = "Azshara";

                if (list[i].AreaName == "Darnassis")
                    list[i].AreaName = "Darnassus";

                if (list[i].AreaName == "Hilsbrad")
                    list[i].AreaName = "Hillsbrad";

                if (list[i].AreaName == "Ogrimmar")
                    list[i].AreaName = "Orgrimmar";

                // The
                if (list[i].AreaName == "Barrens")
                    list[i].AreaName = "TheBarrens";

                if (list[i].AreaName == "Hinterlands")
                    list[i].AreaName = "TheHinterlands";

                // have to test this later
                //if (list[i].AreaName == "Sunwell")
                //    list[i].AreaName = "Isle of Quel'Danas";
            }
        }
    }
}