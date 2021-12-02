﻿/**
 *   Copyright (C) 2021 okaygo
 *
 *   https://github.com/misterokaygo/MapAssist/
 *
 *  This program is free software: you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation, either version 3 of the License, or
 *  (at your option) any later version.
 *
 *  This program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 *  GNU General Public License for more details.
 *
 *  You should have received a copy of the GNU General Public License
 *  along with this program.  If not, see <https://www.gnu.org/licenses/>.
 **/

using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using MapAssist.Settings;
using MapAssist.Types;

namespace MapAssist.Helpers
{
    public static class PointOfInterestHandler
    {
        private static readonly Dictionary<Area, GameObject> AreaSpecificQuestObjects = new Dictionary<Area, GameObject>
        {
            [Area.MatronsDen] = GameObject.SparklyChest, // Lilith
            [Area.FurnaceOfPain] = GameObject.SparklyChest, // Über Izual
        };

        private static readonly HashSet<GameObject> QuestObjects = new HashSet<GameObject>
        {
            GameObject.HoradricCubeChest,
            GameObject.HoradricScrollChest,
            GameObject.StaffOfKingsChest,
            GameObject.HoradricOrifice,
            GameObject.YetAnotherTome, // Summoner in Arcane Sanctuary
            GameObject.FrozenAnya,
            GameObject.InifussTree,
            GameObject.CairnStoneAlpha,
            GameObject.WirtCorpse,
            GameObject.HellForge,
            GameObject.NihlathakWildernessStartPosition
        };

        private static readonly HashSet<GameObject> SuperChests = new HashSet<GameObject>
        {
            GameObject.GoodChest,
            GameObject.SparklyChest,
            GameObject.ArcaneLargeChestLeft,
            GameObject.ArcaneLargeChestRight,
            GameObject.ArcaneSmallChestLeft,
            GameObject.ArcaneSmallChestRight
        };

        private static readonly HashSet<GameObject> NormalChests = new HashSet<GameObject>
        {
            GameObject.LargeChestRight,
            GameObject.LargeChestLeft,
            GameObject.TombLargeChestL,
            GameObject.TombLargeChestR,
            GameObject.Act1LargeChestRight,
            GameObject.Act1TallChestRight,
            GameObject.Act1MediumChestRight,
            GameObject.Act1LargeChest1,
            GameObject.Act2MediumChestRight,
            GameObject.Act2LargeChestRight,
            GameObject.Act2LargeChestLeft,
            GameObject.MediumChestLeft,
            GameObject.LargeChestLeft2,
            GameObject.JungleChest,
            GameObject.JungleMediumChestLeft,
            GameObject.TallChestLeft,
            GameObject.Gchest1L,
            GameObject.Gchest2R,
            GameObject.Gchest3R,
            GameObject.GLchest3L,
            GameObject.MafistoLargeChestLeft,
            GameObject.MafistoLargeChestRight,
            GameObject.MafistoMediumChestLeft,
            GameObject.MafistoMediumChestRight,
            GameObject.SpiderLairLargeChestLeft,
            GameObject.SpiderLairTallChestLeft,
            GameObject.SpiderLairMediumChestRight,
            GameObject.SpiderLairTallChestRight,
            GameObject.HoradricCubeChest,
            GameObject.HoradricScrollChest,
            GameObject.StaffOfKingsChest,
            GameObject.LargeChestR,
            GameObject.InnerHellBoneChest,
            GameObject.KhalimChest1,
            GameObject.KhalimChest2,
            GameObject.KhalimChest3,
            GameObject.ExpansionChestRight,
            GameObject.ExpansionWoodChestLeft,
            GameObject.BurialChestLeft,
            GameObject.BurialChestRight,
            GameObject.ExpansionChestLeft,
            GameObject.ExpansionWoodChestRight,
            GameObject.ExpansionSmallChestLeft,
            GameObject.ExpansionSmallChestRight,
            GameObject.ExpansionExplodingChest,
            GameObject.ExpansionSpecialChest,
            GameObject.ExpansionSnowyWoodChestLeft,
            GameObject.ExpansionSnowyWoodChestRight,
            GameObject.ExpansionSnowyWoodChest2Left,
            GameObject.ExpansionSnowyWoodChest2Right,
            GameObject.NotSoGoodChest,
        };

        private static readonly HashSet<GameObject> ArmorWeapRacks = new HashSet<GameObject>
        {
            GameObject.ExpansionArmorStandRight,
            GameObject.ExpansionArmorStandLeft,
            GameObject.ArmorStandRight,
            GameObject.ArmorStandLeft,
            GameObject.ExpansionWeaponRackRight,
            GameObject.ExpansionWeaponRackLeft,
            GameObject.WeaponRackRight,
            GameObject.WeaponRackLeft,
        };

        private static readonly HashSet<GameObject> Shrines = new HashSet<GameObject>
        {
            GameObject.Shrine,
            GameObject.HornShrine,
            GameObject.ForestAltar,
            GameObject.DesertShrine1,
            GameObject.DesertShrine2,
            GameObject.DesertShrine3,
            GameObject.DesertShrine4,
            GameObject.DesertShrine5,
            GameObject.SteleDesertMagicShrine,
        };

        public static List<PointOfInterest> Get(MapApi mapApi, AreaData areaData)
        {
            var pointOfInterest = new List<PointOfInterest>();

            switch (areaData.Area)
            {
                case Area.CanyonOfTheMagi:
                    // Work out which tomb is the right once. 
                    // Load the maps for all of the tombs, and check which one has the Orifice.
                    // Declare that tomb as point of interest.
                    Area[] tombs = new[]
                    {
                        Area.TalRashasTomb1, Area.TalRashasTomb2, Area.TalRashasTomb3, Area.TalRashasTomb4,
                        Area.TalRashasTomb5, Area.TalRashasTomb6, Area.TalRashasTomb7
                    };
                    var realTomb = Area.None;
                    Parallel.ForEach(tombs, tombArea =>
                    {
                        AreaData tombData = mapApi.GetMapData(tombArea);
                        if (tombData.Objects.ContainsKey(GameObject.HoradricOrifice))
                        {
                            realTomb = tombArea;
                        }
                    });

                    if (realTomb != Area.None && areaData.AdjacentLevels[realTomb].Exits.Any())
                    {
                        pointOfInterest.Add(new PointOfInterest
                        {
                            Label = realTomb.Name(),
                            Position = areaData.AdjacentLevels[realTomb].Exits[0],
                            RenderingSettings = MapAssistConfiguration.Loaded.MapConfiguration.NextArea
                        });
                    }

                    break;
                default:
                    // By default, draw a line to the next highest neighbouring area.
                    // Also draw labels and previous doors for all other areas.
                    if (areaData.AdjacentLevels.Any())
                    {
                        Area highestArea = areaData.AdjacentLevels.Keys.Max();
                        if (highestArea > areaData.Area)
                        {
                            if (areaData.AdjacentLevels[highestArea].Exits.Any())
                            {
                                pointOfInterest.Add(new PointOfInterest
                                {
                                    Label = highestArea.Name(),
                                    Position = areaData.AdjacentLevels[highestArea].Exits[0],
                                    RenderingSettings = MapAssistConfiguration.Loaded.MapConfiguration.NextArea
                                });
                            }
                        }

                        foreach (AdjacentLevel level in areaData.AdjacentLevels.Values)
                        {
                            // Already have something drawn for this.
                            if (level.Area == highestArea)
                            {
                                continue;
                            }

                            foreach (Point position in level.Exits)
                            {
                                pointOfInterest.Add(new PointOfInterest
                                {
                                    Label = level.Area.Name(),
                                    Position = position,
                                    RenderingSettings = MapAssistConfiguration.Loaded.MapConfiguration.PreviousArea
                                });
                            }
                        }
                    }

                    break;
            }

            foreach (KeyValuePair<GameObject, Point[]> objAndPoints in areaData.Objects)
            {
                GameObject obj = objAndPoints.Key;
                Point[] points = objAndPoints.Value;

                if (!points.Any())
                {
                    continue;
                }

                // Waypoints
                if (obj.IsWaypoint())
                {
                    pointOfInterest.Add(new PointOfInterest
                    {
                        Label = obj.ToString(),
                        Position = points[0],
                        RenderingSettings = MapAssistConfiguration.Loaded.MapConfiguration.Waypoint
                    });
                }
                // Quest objects
                else if (QuestObjects.Contains(obj))
                {
                    pointOfInterest.Add(new PointOfInterest
                    {
                        Label = obj.ToString(), 
                        Position = points[0], 
                        RenderingSettings = MapAssistConfiguration.Loaded.MapConfiguration.Quest
                    });
                }
                // Area-specific quest objects
                else if (AreaSpecificQuestObjects.ContainsKey(areaData.Area))
                {
                    if (AreaSpecificQuestObjects[areaData.Area] == obj)
                    {
                        pointOfInterest.Add(new PointOfInterest
                        {
                            Label = obj.ToString(),
                            Position = points[0],
                            RenderingSettings = MapAssistConfiguration.Loaded.MapConfiguration.Quest
                        });
                    }
                }
                // Shrines
                else if (Shrines.Contains(obj))
                {
                    foreach (Point point in points)
                    {
                        pointOfInterest.Add(new PointOfInterest
                        {
                            Label = obj.ToString(), 
                            Position = point, 
                            RenderingSettings = MapAssistConfiguration.Loaded.MapConfiguration.Shrine
                        });
                    }
                }
                // Super Chest
                else if (SuperChests.Contains(obj))
                {
                    foreach (Point point in points)
                    {
                        pointOfInterest.Add(new PointOfInterest
                        {
                            Label = obj.ToString(),
                            Position = point,
                            RenderingSettings = MapAssistConfiguration.Loaded.MapConfiguration.SuperChest
                        });
                    }
                }
                // Normal Chest
                else if (NormalChests.Contains(obj))
                {
                    foreach (Point point in points)
                    {
                        pointOfInterest.Add(new PointOfInterest
                        {
                            Label = obj.ToString(),
                            Position = point,
                            RenderingSettings = MapAssistConfiguration.Loaded.MapConfiguration.NormalChest
                        });
                    }
                }
                // Armor Stands & Weapon Racks
                else if (ArmorWeapRacks.Contains(obj))
                {
                    foreach (Point point in points)
                    {
                        pointOfInterest.Add(new PointOfInterest
                        {
                            Label = obj.ToString(),
                            Position = point,
                            RenderingSettings = MapAssistConfiguration.Loaded.MapConfiguration.ArmorWeapRack
                        });
                    }
                }
            }

            return pointOfInterest;
        }
    }
}
