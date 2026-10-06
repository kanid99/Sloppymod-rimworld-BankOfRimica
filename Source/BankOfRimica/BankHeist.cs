using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace BankOfRimica
{
    /// <summary>A massive raid whose goal is the bank bullion, not the colonists.</summary>
    public class IncidentWorker_BankHeist : IncidentWorker_RaidEnemy
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            Map map = parms.target as Map;
            return map != null && map.listerThings.ThingsOfDef(BoR_DefOf.BoR_Bullion).Any() && base.CanFireNowSub(parms);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            parms.raidStrategy = BoR_DefOf.BoR_BankHeistStrategy;
            // With Vanilla Factions Expanded - Pirates, the robbers may crash in on gauntlet ships,
            // and almost certainly will once the colony holds a million silver for the bank.
            bool gauntlet = parms.raidArrivalMode == null && GauntletCompat.ShouldUse && Rand.Chance(GauntletCompat.Chance(BankUtility.Bank?.custody));
            if (gauntlet)
            {
                parms.raidArrivalMode = GauntletCompat.ArrivalMode;
                // Gauntlet ships belong to the junkers (or pirates/mercenaries if the junkers aren't hostile).
                if (parms.faction == null) parms.faction = GauntletCompat.PreferredFaction();
            }
            // Heists need hands: pick a hostile humanlike faction rather than mechanoids or insects.
            if (parms.faction == null &&
                Find.FactionManager.AllFactions
                    .Where(f => !f.IsPlayer && !f.Hidden && !f.defeated && f.def.humanlikeFaction && f.HostileTo(Faction.OfPlayer))
                    .TryRandomElement(out Faction thieves))
            {
                parms.faction = thieves;
            }
            // Later heists in a long contract come in harder.
            int earlier = BankUtility.Bank?.custody?.heistsLaunched ?? 0;
            float escalation = 1f + BankUtility.Settings.heistEscalation * earlier;
            parms.points = Mathf.Max(parms.points * BankUtility.Settings.heistPointsMultiplier * escalation, 800f);
            return base.TryExecuteWorker(parms);
        }
    }

    public class RaidStrategyWorker_BankHeist : RaidStrategyWorker
    {
        // Only usable when explicitly requested by the heist incident, never picked for ordinary raids.
        public override bool CanUseWith(IncidentParms parms, PawnGroupKindDef groupKind)
        {
            return parms.raidStrategy == def;
        }

        public override List<Pawn> SpawnThreats(IncidentParms parms)
        {
            List<Pawn> pawns = base.SpawnThreats(parms);
            // Gauntlet ships take the pawns out of this list and give them their own assault lord later.
            if (GauntletCompat.IsGauntlet(parms)) GauntletCompat.Track(pawns, parms.target as Map, parms.faction);
            return pawns;
        }

        public override void MakeLords(IncidentParms parms, List<Pawn> pawns)
        {
            // Warcasket troops cover the thieves instead of stealing (gauntlet heists hand pawns over later, see GauntletCompat).
            if (!WarcasketUtility.ShouldSplit(pawns, out List<Pawn> cover, out List<Pawn> thieves))
            {
                base.MakeLords(parms, pawns);
                return;
            }
            base.MakeLords(parms, thieves);
            Lord thiefLord = thieves[0].GetLord();
            LordMaker.MakeNewLord(parms.faction, new LordJob_HeistCover(thiefLord, parms.faction), (Map)parms.target, cover);
        }

        protected override LordJob MakeLordJob(IncidentParms parms, Map map, List<Pawn> pawns, int raidSeed)
        {
            return new LordJob_BankHeist(parms.faction);
        }
    }

    public class LordJob_BankHeist : LordJob
    {
        private Faction faction;

        public LordJob_BankHeist() { }

        public LordJob_BankHeist(Faction faction)
        {
            this.faction = faction;
        }

        public override bool GuiltyOnDowned => true;

        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();

            var heist = new LordToil_BankHeist();
            graph.AddToil(heist);

            var flee = new LordToil_ExitMap(LocomotionUrgency.Jog, false, true);
            flee.useAvoidGrid = true;
            graph.AddToil(flee);

            var lost = new Transition(heist, flee);
            lost.AddTrigger(new Trigger_FractionPawnsLost(0.5f));
            lost.AddPreAction(new TransitionAction_Message("The bank robbers have taken heavy losses and are fleeing with whatever they grabbed."));
            graph.AddTransition(lost);

            var timeout = new Transition(heist, flee);
            timeout.AddTrigger(new Trigger_TicksPassed(GenDate.TicksPerDay));
            timeout.AddPreAction(new TransitionAction_Message("The bank robbers are calling off the heist."));
            graph.AddTransition(timeout);

            return graph;
        }

        public override void ExposeData()
        {
            Scribe_References.Look(ref faction, "faction");
        }
    }

    public class LordToil_BankHeist : LordToil
    {
        public override bool AllowSatisfyLongNeeds => false;

        public override void UpdateAllDuties()
        {
            foreach (Pawn p in lord.ownedPawns)
            {
                p.mindState.duty = new PawnDuty(BoR_DefOf.BoR_BankHeist);
            }
        }
    }

    /// <summary>Grab the closest reachable silver, bashing through doors if need be.</summary>
    public class JobGiver_StealBullion : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn.Map == null || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)) return null;

            if (pawn.carryTracker.CarriedThing != null) return null;

            List<Thing> bullion = pawn.Map.listerThings.ThingsOfDef(BoR_DefOf.BoR_Bullion);
            if (bullion.Count == 0) return null;

            TraverseParms traverse = TraverseParms.For(pawn, Danger.Deadly, TraverseMode.ByPawn, true);
            Thing target = GenClosest.ClosestThing_Global_Reachable(pawn.Position, pawn.Map, bullion, PathEndMode.ClosestTouch, traverse, 9999f,
                t => t.Spawned && !t.IsBurning() && pawn.CanReserve(t));
            if (target == null) return null;

            Job job = JobMaker.MakeJob(BoR_DefOf.BoR_StealBullion, target);
            job.count = target.stackCount;
            job.canBashDoors = true;
            job.locomotionUrgency = LocomotionUrgency.Sprint;
            return job;
        }

    }

    /// <summary>Runs before anything else in the heist duty so a robber with silver never stops to fight.</summary>
    public class JobGiver_EscapeWithBullion : ThinkNode_JobGiver
    {
        /// <summary>Share of robbers who ignore the getaway craft and run for the map edge with their silver.</summary>
        private const float OnFootChance = 0.4f;

        protected override Job TryGiveJob(Pawn pawn)
        {
            Thing carried = pawn.carryTracker?.CarriedThing;
            if (pawn.Map == null || carried == null || carried.def != BoR_DefOf.BoR_Bullion) return null;
            // Each robber sticks to one plan: some go for the craft, some leg it.
            if (Rand.ChanceSeeded(OnFootChance, pawn.thingIDNumber ^ 0x4B3A1)) return ExitOnFoot(pawn);
            return GetawayJob(pawn, allowOnFoot: true);
        }

        public static Job GetawayJob(Pawn pawn, bool allowOnFoot)
        {
            GetawayCraft craft = GetawayUtility.NearestCraft(pawn);
            if (craft != null)
            {
                Job board = JobMaker.MakeJob(BoR_DefOf.BoR_BoardGetaway, craft.thing);
                board.canBashDoors = true;
                board.locomotionUrgency = LocomotionUrgency.Sprint;
                return board;
            }

            // Shuttle still inbound: head for the landing zone and wait for it.
            HeistGetaway g = GetawayUtility.For(pawn.GetLord());
            if (g != null && g.shuttleIncoming && g.landingSpot.IsValid &&
                CellFinder.TryFindRandomCellNear(g.landingSpot, pawn.Map, 6,
                    c => c.Standable(pawn.Map) && !BankShuttleUtility.InLandingRect(g.landingSpot, c) && pawn.CanReach(c, PathEndMode.OnCell, Danger.Deadly, true, true, TraverseMode.ByPawn),
                    out IntVec3 waitAt))
            {
                if (pawn.Position.DistanceTo(g.landingSpot) < 9f) return JobMaker.MakeJob(JobDefOf.Wait_Combat, 120, true);
                Job go = JobMaker.MakeJob(JobDefOf.Goto, waitAt);
                go.canBashDoors = true;
                go.locomotionUrgency = LocomotionUrgency.Sprint;
                go.expiryInterval = 300;
                return go;
            }

            return allowOnFoot ? ExitOnFoot(pawn) : null;
        }

        public static Job ExitOnFoot(Pawn pawn)
        {
            if (!RCellFinder.TryFindBestExitSpot(pawn, out IntVec3 exit, TraverseMode.ByPawn)) return null;
            Job run = JobMaker.MakeJob(JobDefOf.Goto, exit);
            run.exitMapOnArrival = true;
            run.canBashDoors = true;
            run.locomotionUrgency = LocomotionUrgency.Sprint;
            return run;
        }
    }
}
