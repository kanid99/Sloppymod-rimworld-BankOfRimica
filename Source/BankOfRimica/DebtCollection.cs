using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace BankOfRimica
{
    public class WorldObjectCompProperties_DebtCollection : WorldObjectCompProperties
    {
        public WorldObjectCompProperties_DebtCollection()
        {
            compClass = typeof(WorldObjectComp_DebtCollection);
        }
    }

    /// <summary>Adds a "Collect debt" button for caravans visiting the bank's current debtor.</summary>
    public class WorldObjectComp_DebtCollection : WorldObjectComp
    {
        public override IEnumerable<Gizmo> GetCaravanGizmos(Caravan caravan)
        {
            BankComponent bank = BankUtility.Bank;
            CollectionContract c = bank?.collection;
            if (c == null || c.target != parent) yield break;

            var cmd = new Command_Action
            {
                defaultLabel = "Collect debt",
                defaultDesc = $"Press {parent.LabelCap} to pay its {BankUtility.Money(c.debt)} debt to the {BankUtility.BankName}. " +
                              $"Success chance: {BankComponent.CollectionChance(caravan, BestCaravanPawnUtility.FindBestNegotiator(caravan)).ToStringPercent()} (negotiation ability and caravan muscle). " +
                              "Either way, the settlement's faction will resent it.",
                icon = TexCommand.Attack,
                action = () => bank.AttemptCollection(caravan),
            };
            if (c.attempted) cmd.Disable("They already refused. Seize their assets by force instead.");
            yield return cmd;
        }

        public override string CompInspectStringExtra()
        {
            CollectionContract c = BankUtility.Bank?.collection;
            if (c == null || c.target != parent) return null;
            return $"Owes the {BankUtility.BankName} {BankUtility.Money(c.debt)} (your collection contract)";
        }
    }
}
