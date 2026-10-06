using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace BankOfRimica
{
    /// <summary>Detects VFE Pirates warcaskets without referencing its assembly.</summary>
    public static class WarcasketUtility
    {
        private static bool looked;
        private static System.Type warcasketType;

        public static bool IsWarcasket(Pawn p)
        {
            if (!looked)
            {
                looked = true;
                warcasketType = GenTypes.GetTypeInAnyAssembly("VFEPirates.Apparel_Warcasket");
            }
            if (warcasketType == null || p?.apparel == null) return false;
            return p.apparel.WornApparel.Any(a => warcasketType.IsInstanceOfType(a));
        }

        /// <summary>Splits off a warcasket cover force, but only when there are also foot raiders to do the stealing.</summary>
        public static bool ShouldSplit(List<Pawn> pawns, out List<Pawn> cover, out List<Pawn> thieves)
        {
            cover = pawns.Where(IsWarcasket).ToList();
            thieves = pawns.Where(p => !IsWarcasket(p)).ToList();
            return cover.Count > 0 && thieves.Count > 0;
        }
    }

    /// <summary>
    /// Warcasket troops covering a bank heist: assault the colony while the thieves work, then, once every
    /// thief has left (boarded, escaped or died), fall back to the getaway craft and leave with it.
    /// </summary>
    public class LordJob_HeistCover : LordJob
    {
        public Lord thiefLord;
        private Faction faction;

        public LordJob_HeistCover() { }

        public LordJob_HeistCover(Lord thiefLord, Faction faction)
        {
            this.thiefLord = thiefLord;
            this.faction = faction;
        }

        public override bool GuiltyOnDowned => true;

        public override StateGraph CreateGraph()
        {
            var graph = new StateGraph();
            var assault = new LordToil_AssaultColony();
            graph.AddToil(assault);
            var board = new LordToil_HeistCoverBoard();
            graph.AddToil(board);

            var done = new Transition(assault, board);
            done.AddTrigger(new Trigger_ThievesGone());
            done.AddTrigger(new Trigger_TicksPassed(GenDate.TicksPerDay));
            done.AddPreAction(new TransitionAction_Message("The warcaskets are falling back to their ships."));
            graph.AddTransition(done);
            return graph;
        }

        public override void ExposeData()
        {
            Scribe_References.Look(ref thiefLord, "thiefLord");
            Scribe_References.Look(ref faction, "faction");
        }
    }

    public class Trigger_ThievesGone : Trigger
    {
        public override bool ActivateOn(Lord lord, TriggerSignal signal)
        {
            if (signal.type != TriggerSignalType.Tick || Find.TickManager.TicksGame % 60 != 0) return false;
            return lord.LordJob is LordJob_HeistCover cover && !GetawayUtility.LordActive(cover.thiefLord, lord.Map);
        }
    }

    public class LordToil_HeistCoverBoard : LordToil
    {
        public override bool AllowSatisfyLongNeeds => false;

        public override void UpdateAllDuties()
        {
            foreach (Pawn p in lord.ownedPawns)
            {
                p.mindState.duty = new PawnDuty(BoR_DefOf.BoR_HeistCoverBoard);
            }
        }
    }

    /// <summary>Warcaskets heading home: board the getaway craft (calling it in if needed), or walk off the map.</summary>
    public class JobGiver_CoverBoardGetaway : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn.Map == null) return null;
            if (GetawayUtility.For(pawn.GetLord()) == null) GetawayUtility.Request(pawn);
            return JobGiver_EscapeWithBullion.GetawayJob(pawn, allowOnFoot: false);
        }
    }
}
