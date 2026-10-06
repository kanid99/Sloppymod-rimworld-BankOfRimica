using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace BankOfRimica
{
    public enum ShuttleMission { Deliver, Collect }

    /// <summary>The bank's storage pallet. Only holds bank silver bars and can't be taken apart by the colony.</summary>
    public class Building_BankPallet : Building_Storage
    {
        public override AcceptanceReport DeconstructibleBy(Faction faction) => "Property of the " + BankUtility.BankName + ".";
    }

    /// <summary>Incoming bank shuttle. On touchdown it becomes a landed shuttle that does the job.</summary>
    public class Skyfaller_BankShuttle : Skyfaller
    {
        public ShuttleMission mission;
        public IntVec3 palletCell = IntVec3.Invalid;
        public int bars;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref mission, "mission");
            Scribe_Values.Look(ref palletCell, "palletCell", IntVec3.Invalid);
            Scribe_Values.Look(ref bars, "bars");
        }

        protected override void Impact()
        {
            Map map = Map;
            IntVec3 pos = Position;
            base.Impact();
            var landed = (Building_BankShuttle)ThingMaker.MakeThing(BoR_DefOf.BoR_BankShuttleLanded);
            landed.mission = mission;
            landed.palletCell = palletCell;
            landed.bars = bars;
            GenSpawn.Spawn(landed, pos, map, WipeMode.VanishOrMoveAside);
        }
    }

    public class Skyfaller_BankShuttleLeaving : Skyfaller
    {
        protected override void LeaveMap()
        {
            if (!Destroyed) Destroy();
        }
    }

    /// <summary>A landed bank shuttle: unloads (or loads) the pallet, then takes off.</summary>
    public class Building_BankShuttle : Building
    {
        private const int WorkTick = 120;
        private const int LeaveTick = 300;

        public ShuttleMission mission;
        public IntVec3 palletCell = IntVec3.Invalid;
        public int bars;
        private int age;

        public override AcceptanceReport DeconstructibleBy(Faction faction) => false;
        private bool done;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref mission, "mission");
            Scribe_Values.Look(ref palletCell, "palletCell", IntVec3.Invalid);
            Scribe_Values.Look(ref bars, "bars");
            Scribe_Values.Look(ref age, "age");
            Scribe_Values.Look(ref done, "done");
        }

        protected override void Tick()
        {
            base.Tick();
            if (!Spawned) return;
            age++;
            if (age == WorkTick && !done)
            {
                done = true;
                if (mission == ShuttleMission.Deliver) Deliver();
                else Collect();
            }
            if (age >= LeaveTick)
            {
                Map map = Map;
                IntVec3 pos = Position;
                Destroy(DestroyMode.Vanish);
                SkyfallerMaker.SpawnSkyfaller(BoR_DefOf.BoR_BankShuttleLeaving, pos, map);
            }
        }

        private void Deliver()
        {
            Map map = Map;
            IntVec3 cell = palletCell.IsValid && palletCell.InBounds(map) ? palletCell : BankShuttleUtility.FallbackPalletCell(map, Position);

            Thing pallet = cell.GetThingList(map).FirstOrDefault(t => t.def == BoR_DefOf.BoR_BankPallet);
            if (pallet == null)
            {
                pallet = ThingMaker.MakeThing(BoR_DefOf.BoR_BankPallet);
                pallet.SetFaction(Faction.OfPlayer);
                GenSpawn.Spawn(pallet, cell, map, WipeMode.VanishOrMoveAside);
            }

            int remaining = bars;
            while (remaining > 0)
            {
                Thing stack = ThingMaker.MakeThing(BoR_DefOf.BoR_Bullion);
                stack.stackCount = Mathf.Min(remaining, stack.def.stackLimit);
                remaining -= stack.stackCount;
                GenPlace.TryPlaceThing(stack, cell, map, ThingPlaceMode.Near);
            }
            FleckMaker.ThrowDustPuff(cell.ToVector3Shifted(), map, 1.5f);
            Messages.Message($"The {BankUtility.BankName} shuttle has unloaded the bank's silver onto its pallet.",
                new LookTargets(pallet), MessageTypeDefOf.NeutralEvent);
        }

        private void Collect()
        {
            Map map = Map;
            foreach (Thing pallet in map.listerThings.ThingsOfDef(BoR_DefOf.BoR_BankPallet).ToList())
            {
                FleckMaker.ThrowDustPuff(pallet.Position.ToVector3Shifted(), map, 1.5f);
                pallet.Destroy(DestroyMode.Vanish);
            }
            BankUtility.Bank?.SettleCustody();
        }
    }

    public static class BankShuttleUtility
    {
        private static readonly IntVec2 ShuttleSize = new IntVec2(3, 3);

        /// <summary>Open, unroofed 3x3 ground near the pallet, not covering the pallet itself.</summary>
        public static bool TryFindLandingSpot(Map map, IntVec3 near, out IntVec3 spot)
        {
            foreach (IntVec3 c in GenRadial.RadialCellsAround(near, 25f, false))
            {
                CellRect rect = GenAdj.OccupiedRect(c, Rot4.North, ShuttleSize);
                if (rect.Contains(near) || rect.ExpandedBy(1).Contains(near)) continue;
                if (rect.Cells.All(x => GoodLandingCell(map, x)))
                {
                    spot = c;
                    return true;
                }
            }
            spot = IntVec3.Invalid;
            return false;
        }

        private static bool GoodLandingCell(Map map, IntVec3 c)
        {
            return c.InBounds(map) && !c.Fogged(map) && c.Standable(map) && !c.Roofed(map) &&
                   c.GetEdifice(map) == null && !c.GetThingList(map).Any(t => t.def.category == ThingCategory.Building || t is Pawn);
        }

        public static IntVec3 FallbackPalletCell(Map map, IntVec3 near)
        {
            return CellFinder.TryFindRandomCellNear(near, map, 6, c => c.Standable(map) && c.GetFirstBuilding(map) == null, out IntVec3 r) ? r : near;
        }

        /// <summary>Sends a bank shuttle to the pallet. Returns false if nowhere to land.</summary>
        public static bool SendShuttle(Map map, IntVec3 palletCell, ShuttleMission mission, int bars)
        {
            if (map == null) return false;
            IntVec3 target = palletCell.IsValid ? palletCell : DropCellFinder.TradeDropSpot(map);
            if (!TryFindLandingSpot(map, target, out IntVec3 spot))
            {
                spot = DropCellFinder.TradeDropSpot(map);
            }
            var shuttle = (Skyfaller_BankShuttle)SkyfallerMaker.SpawnSkyfaller(BoR_DefOf.BoR_BankShuttleIncoming, spot, map);
            shuttle.mission = mission;
            shuttle.palletCell = palletCell;
            shuttle.bars = bars;
            return true;
        }
    }

    /// <summary>Lets the player choose where the bank puts its pallet when accepting a custody contract.</summary>
    public class Designator_PlaceBankPallet : Designator
    {
        private readonly Map targetMap;
        private readonly CustodyContract offer;
        private static readonly Color GhostGood = new Color(0.5f, 1f, 0.6f, 0.45f);

        public Designator_PlaceBankPallet(Map map, CustodyContract offer)
        {
            targetMap = map;
            this.offer = offer;
            defaultLabel = "Place bank pallet";
            defaultDesc = $"Choose where the {BankUtility.BankName} will set down its pallet of {offer.bullionCount} silver bars. The bank's shuttle needs open, unroofed ground nearby to land.";
            icon = ContentFinder<Texture2D>.Get("Things/Building/BoR_BankPallet");
            useMouseIcon = true;
        }

        public override AcceptanceReport CanDesignateCell(IntVec3 c)
        {
            Map map = Map;
            if (map != targetMap) return "Must be placed in the colony that accepted the contract.";
            if (!c.InBounds(map) || c.Fogged(map)) return false;
            if (!c.Standable(map)) return "Not standable.";
            if (c.GetFirstBuilding(map) != null) return "Space occupied.";
            if (!BankShuttleUtility.TryFindLandingSpot(map, c, out _)) return "No open, unroofed space nearby for the bank shuttle to land.";
            return true;
        }

        public override void DesignateSingleCell(IntVec3 c)
        {
            Find.DesignatorManager.Deselect();
            BankUtility.Bank?.TryAcceptCustody(targetMap, offer, c);
        }

        public override void SelectedUpdate()
        {
            IntVec3 c = UI.MouseCell();
            if (CanDesignateCell(c).Accepted)
            {
                GhostDrawer.DrawGhostThing(c, Rot4.North, BoR_DefOf.BoR_BankPallet, null, GhostGood, AltitudeLayer.Blueprint);
            }
        }
    }
}
