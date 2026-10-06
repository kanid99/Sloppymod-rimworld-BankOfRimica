using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace BankOfRimica
{
    /// <summary>One craft robbers can board: our getaway shuttle, or a crashed VFE Pirates gauntlet ship.</summary>
    public class GetawayCraft : IExposable
    {
        public Thing thing;
        public IntVec3 pos;
        public List<Pawn> aboard = new List<Pawn>();

        public void ExposeData()
        {
            Scribe_References.Look(ref thing, "thing");
            Scribe_Values.Look(ref pos, "pos");
            Scribe_Collections.Look(ref aboard, "aboard", LookMode.Reference);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                aboard ??= new List<Pawn>();
                aboard.RemoveAll(p => p == null);
            }
        }
    }

    /// <summary>
    /// A heist's way out. Robbers carrying silver board a craft and wait inside (as world pawns) until it lifts off.
    /// Destroy the craft first and everyone aboard spills out with what they took; once it leaves, they're gone.
    /// </summary>
    public class HeistGetaway : IExposable
    {
        public int id;
        public Lord lord;
        public Faction faction;
        public int mapId;
        public List<GetawayCraft> crafts = new List<GetawayCraft>();
        public bool shuttleIncoming;
        public IntVec3 landingSpot = IntVec3.Invalid;
        public int createdTick;
        public int firstBoardTick = -1;

        public Map Map => Find.Maps.Find(m => m.uniqueID == mapId);

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_References.Look(ref lord, "lord");
            Scribe_References.Look(ref faction, "faction");
            Scribe_Values.Look(ref mapId, "mapId");
            Scribe_Collections.Look(ref crafts, "crafts", LookMode.Deep);
            Scribe_Values.Look(ref shuttleIncoming, "shuttleIncoming");
            Scribe_Values.Look(ref landingSpot, "landingSpot", IntVec3.Invalid);
            Scribe_Values.Look(ref createdTick, "createdTick");
            Scribe_Values.Look(ref firstBoardTick, "firstBoardTick", -1);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) crafts ??= new List<GetawayCraft>();
        }
    }

    public static class GetawayUtility
    {
        private const int WaitAfterFirstBoarding = GenDate.TicksPerHour;
        private const int MaxWait = GenDate.TicksPerDay;

        private static List<HeistGetaway> All => BankUtility.Bank.getaways;

        public static bool IsGauntletShip(Thing t) => t.def.defName.StartsWith("VFEP_CrashedShip");

        /// <summary>Warcasket cover lords share their thieves' getaway.</summary>
        public static Lord ThiefLordOf(Lord lord) =>
            lord?.LordJob is LordJob_HeistCover cover && cover.thiefLord != null ? cover.thiefLord : lord;

        public static HeistGetaway For(Lord lord)
        {
            lord = ThiefLordOf(lord);
            return lord == null ? null : All.Find(g => g.lord == lord);
        }

        public static Lord CoverLordFor(HeistGetaway g, Map map) =>
            map.lordManager.lords.FirstOrDefault(l => l.LordJob is LordJob_HeistCover c && c.thiefLord == g.lord);

        public static bool LordActive(Lord lord, Map map) =>
            lord != null && map != null && map.lordManager.lords.Contains(lord) &&
            lord.ownedPawns.Any(p => p.Spawned && !p.Downed && !p.Dead);

        /// <summary>Called when a robber first gets their hands on silver (or the cover force wants to leave).</summary>
        public static void Request(Pawn thief)
        {
            Lord lord = ThiefLordOf(thief.GetLord());
            if (lord == null || For(lord) != null) return;
            BankComponent bank = BankUtility.Bank;
            var g = new HeistGetaway
            {
                id = bank.NextGetawayId(),
                lord = lord,
                faction = thief.Faction,
                mapId = thief.Map.uniqueID,
                createdTick = Find.TickManager.TicksGame,
            };
            All.Add(g);

            // Gauntlet heist: the junker ships they crashed in on are the way out.
            foreach (Thing ship in thief.Map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial))
            {
                if (IsGauntletShip(ship) && ship.Faction == thief.Faction && ship.Spawned)
                    g.crafts.Add(new GetawayCraft { thing = ship, pos = ship.Position });
            }
            if (g.crafts.Count > 0)
            {
                Messages.Message("The bank robbers are hauling the silver back to their gauntlet ships. Destroy the ships before they take off!",
                    new LookTargets(g.crafts.Select(c => c.thing)), MessageTypeDefOf.ThreatBig);
                return;
            }

            // Otherwise call in a getaway shuttle somewhere outside the home area.
            Map map = thief.Map;
            if (!TryFindGetawaySpot(thief, out IntVec3 spot)) return;
            g.shuttleIncoming = true;
            g.landingSpot = spot;
            var sky = (Skyfaller_HeistShuttle)SkyfallerMaker.SpawnSkyfaller(BoR_DefOf.BoR_HeistShuttleIncoming, spot, map);
            sky.getawayId = g.id;
            sky.SetFaction(thief.Faction);
            Messages.Message("The bank robbers have called in a getaway shuttle. Destroy it before it takes off with your silver!",
                new LookTargets(spot, map), MessageTypeDefOf.ThreatBig);
        }

        private static bool TryFindGetawaySpot(Pawn thief, out IntVec3 spot)
        {
            Map map = thief.Map;
            Area home = map.areaManager.Home;
            bool Good(IntVec3 c)
            {
                if (home[c]) return false;
                if (!BankShuttleUtility.LandingRectClear(map, c)) return false;
                return map.reachability.CanReach(thief.Position, c, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors));
            }
            if (CellFinder.TryFindRandomCellNear(thief.Position, map, 45, Good, out spot)) return true;
            return BankShuttleUtility.TryFindLandingSpot(map, thief.Position, out spot);
        }

        public static void Notify_ShuttleLanded(int id, Building_HeistShuttle shuttle)
        {
            HeistGetaway g = All.Find(x => x.id == id);
            if (g == null) return;
            g.shuttleIncoming = false;
            g.crafts.Add(new GetawayCraft { thing = shuttle, pos = shuttle.Position });
        }

        public static GetawayCraft NearestCraft(Pawn pawn)
        {
            HeistGetaway g = For(pawn.GetLord());
            if (g == null) return null;
            return g.crafts
                .Where(c => c.thing != null && c.thing.Spawned && c.thing.Map == pawn.Map &&
                            pawn.CanReach(c.thing, PathEndMode.Touch, Danger.Deadly, true, true, TraverseMode.ByPawn))
                .OrderBy(c => c.thing.Position.DistanceToSquared(pawn.Position))
                .FirstOrDefault();
        }

        /// <summary>The robber climbs aboard and waits, out of reach but not yet gone.</summary>
        public static void Board(Pawn pawn, Thing craftThing)
        {
            Lord lord = pawn.GetLord();
            HeistGetaway g = For(lord);
            GetawayCraft craft = g?.crafts.Find(c => c.thing == craftThing);
            if (craft == null || !craftThing.Spawned) return;
            lord?.Notify_PawnLost(pawn, PawnLostCondition.ExitedMap);
            pawn.DeSpawn();
            Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
            craft.aboard.Add(pawn);
            if (g.firstBoardTick < 0) g.firstBoardTick = Find.TickManager.TicksGame;
        }

        public static void Tick()
        {
            BankComponent bank = BankUtility.Bank;
            if (bank == null || bank.getaways.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            for (int i = bank.getaways.Count - 1; i >= 0; i--)
            {
                HeistGetaway g = bank.getaways[i];
                Map map = g.Map;
                if (map == null)
                {
                    foreach (GetawayCraft c in g.crafts) LoseForever(c.aboard);
                    bank.getaways.RemoveAt(i);
                    continue;
                }

                // Crafts shot down before take-off spill their passengers.
                for (int c = g.crafts.Count - 1; c >= 0; c--)
                {
                    GetawayCraft craft = g.crafts[c];
                    if (craft.thing != null && craft.thing.Spawned) continue;
                    Eject(g, craft, map);
                    g.crafts.RemoveAt(c);
                }

                bool thievesLeft = LordActive(g.lord, map);
                bool coverLeft = LordActive(CoverLordFor(g, map), map);
                bool anyAboard = g.crafts.Any(c => c.aboard.Count > 0);
                // With a warcasket cover force, the craft waits for them; they only board once the thieves are gone.
                bool depart = g.crafts.Count > 0 &&
                              ((!thievesLeft && !coverLeft) ||
                               (anyAboard && !coverLeft && now - g.firstBoardTick >= WaitAfterFirstBoarding) ||
                               now - g.createdTick >= MaxWait);
                bool robbersLeft = thievesLeft || coverLeft;
                if (depart)
                {
                    Depart(g, map);
                    bank.getaways.RemoveAt(i);
                }
                else if (g.crafts.Count == 0 && !g.shuttleIncoming && !robbersLeft)
                {
                    bank.getaways.RemoveAt(i);
                }
            }
        }

        private static void Eject(HeistGetaway g, GetawayCraft craft, Map map)
        {
            if (craft.aboard.Count == 0) return;
            Lord lord = g.lord != null && map.lordManager.lords.Contains(g.lord)
                ? g.lord
                : LordMaker.MakeNewLord(g.faction, new LordJob_BankHeist(g.faction), map);
            g.lord = lord;
            foreach (Pawn p in craft.aboard)
            {
                if (p == null || p.Destroyed) continue;
                if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemovePawn(p);
                IntVec3 cell = CellFinder.TryFindRandomCellNear(craft.pos, map, 3, c => c.Standable(map), out IntVec3 r) ? r : craft.pos;
                GenSpawn.Spawn(p, cell, map);
                lord.AddPawn(p);
            }
            Messages.Message($"The getaway craft is down! {craft.aboard.Count} robbers scramble out of the wreck.",
                new LookTargets(craft.pos, map), MessageTypeDefOf.PositiveEvent);
            craft.aboard.Clear();
        }

        private static void Depart(HeistGetaway g, Map map)
        {
            int bars = 0;
            foreach (GetawayCraft craft in g.crafts)
            {
                bars += craft.aboard.Sum(BarsHeldBy);
                LoseForever(craft.aboard);
                craft.aboard.Clear();
                if (craft.thing == null || !craft.thing.Spawned) continue;

                Graphic flying = FlyingGraphicFor(craft.thing);
                IntVec3 pos = craft.thing.Position;
                craft.thing.Destroy(DestroyMode.Vanish);
                var leaving = (Skyfaller_GetawayLeaving)SkyfallerMaker.SpawnSkyfaller(BoR_DefOf.BoR_GetawayLeaving, pos, map);
                leaving.flyingGraphic = flying;
            }
            if (bars > 0)
            {
                Find.LetterStack.ReceiveLetter("Robbers escaped",
                    $"The bank robbers got away with {bars} silver bars ({BankUtility.Money(bars * BankUtility.BullionUnitValue)}). " +
                    $"The {BankUtility.BankName} will charge you for every one of them.",
                    LetterDefOf.NegativeEvent, new LookTargets(g.crafts.Select(c => new TargetInfo(c.pos, map))));
            }
        }

        private static Graphic FlyingGraphicFor(Thing craft)
        {
            if (IsGauntletShip(craft))
            {
                ThingDef ship = DefDatabase<ThingDef>.GetNamedSilentFail(craft.def.defName.Replace("CrashedShip", "Ship"));
                if (ship?.graphic != null) return ship.graphic;
            }
            return craft.Graphic;
        }

        private static int BarsHeldBy(Pawn p)
        {
            int n = 0;
            if (p.carryTracker?.CarriedThing?.def == BoR_DefOf.BoR_Bullion) n += p.carryTracker.CarriedThing.stackCount;
            if (p.inventory != null) n += p.inventory.innerContainer.Where(t => t.def == BoR_DefOf.BoR_Bullion).Sum(t => t.stackCount);
            return n;
        }

        /// <summary>Gone for good: the bars vanish with them and they go back to being ordinary world pawns.</summary>
        private static void LoseForever(List<Pawn> pawns)
        {
            foreach (Pawn p in pawns)
            {
                if (p == null || p.Destroyed) continue;
                Thing carried = p.carryTracker?.CarriedThing;
                if (carried?.def == BoR_DefOf.BoR_Bullion) carried.Destroy();
                p.inventory?.innerContainer.Where(t => t.def == BoR_DefOf.BoR_Bullion).ToList().ForEach(t => t.Destroy());
                if (Find.WorldPawns.Contains(p)) Find.WorldPawns.RemovePawn(p);
                Find.WorldPawns.PassToWorld(p);
            }
        }
    }

    /// <summary>Incoming getaway shuttle; becomes a landed, destroyable shuttle.</summary>
    public class Skyfaller_HeistShuttle : Skyfaller
    {
        public int getawayId;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref getawayId, "getawayId");
        }

        protected override void Impact()
        {
            Map map = Map;
            IntVec3 pos = Position;
            Faction faction = Faction;
            base.Impact();
            var landed = (Building_HeistShuttle)ThingMaker.MakeThing(BoR_DefOf.BoR_HeistShuttleLanded);
            landed.SetFaction(faction);
            GenSpawn.Spawn(landed, pos, map, WipeMode.VanishOrMoveAside);
            GetawayUtility.Notify_ShuttleLanded(getawayId, landed);
        }
    }

    public class Building_HeistShuttle : Building
    {
        public override AcceptanceReport DeconstructibleBy(Faction faction) => false;
    }

    /// <summary>A getaway craft lifting off, drawn with whatever art the craft had (gauntlet ships use their flight art).</summary>
    public class Skyfaller_GetawayLeaving : Skyfaller
    {
        public Graphic flyingGraphic;

        public override Graphic Graphic => flyingGraphic ?? base.Graphic;

        protected override void LeaveMap()
        {
            if (!Destroyed) Destroy();
        }
    }

    public class JobDriver_StealBullion : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A);
            Toil call = ToilMaker.MakeToil("CallGetaway");
            call.initAction = () => GetawayUtility.Request(pawn);
            call.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return call;
        }
    }

    public class JobDriver_BoardGetaway : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil board = ToilMaker.MakeToil("Board");
            board.initAction = () => GetawayUtility.Board(pawn, job.GetTarget(TargetIndex.A).Thing);
            board.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return board;
        }
    }
}
