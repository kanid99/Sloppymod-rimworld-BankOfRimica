using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace BankOfRimica
{
    public static class BankUtility
    {
        public const string BankName = "Bank of Rimica";

        public static BankSettings Settings => BankOfRimicaMod.Settings;

        public static BankComponent Bank => Current.Game?.GetComponent<BankComponent>();

        /// <summary>Silver sitting near powered orbital trade beacons on this map.</summary>
        public static int SilverInBeaconRange(Map map)
        {
            if (map == null) return 0;
            return TradeUtility.AllLaunchableThingsForTrade(map)
                .Where(t => t.def == ThingDefOf.Silver)
                .Sum(t => t.stackCount);
        }

        /// <summary>Launches silver from beacon range to the bank. Returns false if there isn't enough.</summary>
        public static bool TryTakeSilver(Map map, int amount)
        {
            if (amount <= 0) return true;
            if (SilverInBeaconRange(map) < amount) return false;
            TradeUtility.LaunchSilver(map, amount);
            return true;
        }

        /// <summary>Delivers silver (or any stackable thing) by drop pod near the colony's trade drop spot.</summary>
        public static void DropToColony(Map map, ThingDef def, int amount)
        {
            if (map == null || amount <= 0) return;
            var things = new List<Thing>();
            int remaining = amount;
            while (remaining > 0)
            {
                Thing t = ThingMaker.MakeThing(def);
                t.stackCount = Mathf.Min(remaining, def.stackLimit);
                remaining -= t.stackCount;
                things.Add(t);
            }
            IntVec3 spot = DropCellFinder.TradeDropSpot(map);
            DropPodUtility.DropThingsNear(spot, map, things);
        }

        public static float ColonyWealth(Map map)
        {
            if (map != null) return map.wealthWatcher.WealthTotal;
            return WealthUtility.PlayerWealth;
        }

        public static IEnumerable<Map> PlayerHomeMaps => Find.Maps.Where(m => m.IsPlayerHome);

        public static Map RichestHomeMap()
        {
            return PlayerHomeMaps
                .Where(m => m.mapPawns.FreeColonistsSpawnedCount > 0)
                .OrderByDescending(m => m.wealthWatcher.WealthTotal)
                .FirstOrDefault();
        }

        public static string Money(float amount) => Mathf.FloorToInt(amount).ToString("N0") + " silver";

        public static string Days(int ticks) => (ticks / (float)GenDate.TicksPerDay).ToString("0.#") + " days";

        /// <summary>Fires a vanilla enemy raid with custom flavour text.</summary>
        public static bool FireRaid(Map map, float pointsMultiplier, string letterLabel, string letterText)
        {
            if (map == null) return false;
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
            parms.forced = true;
            parms.points *= pointsMultiplier;
            if (!letterLabel.NullOrEmpty()) parms.customLetterLabel = letterLabel;
            if (!letterText.NullOrEmpty()) parms.customLetterText = letterText;
            return IncidentDefOf.RaidEnemy.Worker.TryExecute(parms);
        }

        /// <summary>Counts (and optionally removes) every bank bullion bar the player controls.</summary>
        public static int CountPlayerBullion(bool destroy)
        {
            ThingDef def = BoR_DefOf.BoR_Bullion;
            var found = new List<Thing>();
            foreach (Map map in Find.Maps)
            {
                var onMap = new List<Thing>();
                ThingOwnerUtility.GetAllThingsRecursively(map, ThingRequest.ForDef(def), onMap, false);
                found.AddRange(onMap);
            }
            foreach (Caravan caravan in Find.WorldObjects.Caravans)
            {
                if (caravan.Faction != Faction.OfPlayer) continue;
                found.AddRange(CaravanInventoryUtility.AllInventoryItems(caravan).Where(t => t.def == def));
            }
            int count = found.Distinct().Sum(t => t.stackCount);
            if (destroy)
            {
                foreach (Thing t in found.Distinct().ToList())
                {
                    if (!t.Destroyed) t.Destroy();
                }
            }
            return count;
        }

        public static int BullionUnitValue => Mathf.RoundToInt(BoR_DefOf.BoR_Bullion.BaseMarketValue);
    }
}
