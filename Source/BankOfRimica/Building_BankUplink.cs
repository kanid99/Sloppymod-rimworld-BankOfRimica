using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace BankOfRimica
{
    /// <summary>A powered terminal a colonist uses to talk to the Bank of Rimica.</summary>
    public class Building_BankUplink : Building
    {
        private CompPowerTrader power;

        public bool CanUseNow => Spawned && (power == null || power.PowerOn);

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            power = GetComp<CompPowerTrader>();
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            foreach (FloatMenuOption o in base.GetFloatMenuOptions(selPawn)) yield return o;

            string label = $"Contact the {BankUtility.BankName}";
            if (!CanUseNow)
            {
                yield return new FloatMenuOption(label + " (no power)", null);
                yield break;
            }
            if (!selPawn.CanReach(this, PathEndMode.InteractionCell, Danger.Some))
            {
                yield return new FloatMenuOption(label + " (" + "NoPath".Translate() + ")", null);
                yield break;
            }
            if (!selPawn.health.capacities.CapableOf(PawnCapacityDefOf.Talking))
            {
                yield return new FloatMenuOption(label + " (cannot talk)", null);
                yield break;
            }
            yield return FloatMenuUtility.DecoratePrioritizedTask(new FloatMenuOption(label, () =>
            {
                Job job = JobMaker.MakeJob(BoR_DefOf.BoR_UseBankUplink, this);
                selPawn.jobs.TryTakeOrderedJob(job);
            }), selPawn, this);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo g in base.GetGizmos()) yield return g;
            if (Prefs.DevMode)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Open bank",
                    action = () => Find.WindowStack.Add(new Dialog_BankOfRimica(null, this)),
                };
            }
        }

        public override string GetInspectString()
        {
            string s = base.GetInspectString();
            BankComponent bank = BankUtility.Bank;
            if (bank == null) return s;
            string extra = "Savings: " + BankUtility.Money(bank.balance);
            if (bank.loan != null) extra += "\nLoan owed: " + BankUtility.Money(bank.loan.owed);
            if (bank.bountyActive) extra += "\nBOUNTY CONTRACT: " + BankUtility.Money(bank.bountyOwed) + " owed";
            if (bank.custody != null) extra += "\nVault custody ends in " + BankUtility.Days(bank.custody.endTick - Find.TickManager.TicksGame);
            return s.NullOrEmpty() ? extra : s + "\n" + extra;
        }
    }

    public class JobDriver_UseBankUplink : JobDriver
    {
        private Building_BankUplink Uplink => (Building_BankUplink)job.GetTarget(TargetIndex.A).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => !Uplink.CanUseNow);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
            Toil open = ToilMaker.MakeToil("OpenBank");
            open.initAction = () =>
            {
                if (Uplink.CanUseNow) Find.WindowStack.Add(new Dialog_BankOfRimica(pawn, Uplink));
            };
            open.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return open;
        }
    }
}
