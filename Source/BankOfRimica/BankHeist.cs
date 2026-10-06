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
            // Heists need hands: pick a hostile humanlike faction rather than mechanoids or insects.
            if (parms.faction == null &&
                Find.FactionManager.AllFactions
                    .Where(f => !f.IsPlayer && !f.Hidden && !f.defeated && f.def.humanlikeFaction && f.HostileTo(Faction.OfPlayer))
                    .TryRandomElement(out Faction thieves))
            {
                parms.faction = thieves;
            }
            parms.points = Mathf.Max(parms.points * BankUtility.Settings.heistPointsMultiplier, 800f);
            // With Vanilla Factions Expanded - Pirates, the robbers crash in on gauntlet ships.
            if (parms.raidArrivalMode == null && GauntletCompat.ShouldUse)
            {
                parms.raidArrivalMode = GauntletCompat.ArrivalMode;
            }
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

    /// <summary>Go for the closest reachable bank bullion, bashing through doors if need be, and run off the map with it.</summary>
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

            if (!RCellFinder.TryFindBestExitSpot(pawn, out IntVec3 exit, TraverseMode.ByPawn)) return null;

            Job job = JobMaker.MakeJob(JobDefOf.Steal, target, exit);
            job.count = target.stackCount;
            job.canBashDoors = true;
            job.locomotionUrgency = LocomotionUrgency.Sprint;
            return job;
        }
    }
}
