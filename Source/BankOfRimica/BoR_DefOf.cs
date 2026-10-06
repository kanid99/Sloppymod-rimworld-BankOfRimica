using RimWorld;
using Verse;
using Verse.AI;

namespace BankOfRimica
{
    [DefOf]
    public static class BoR_DefOf
    {
        public static ThingDef BoR_Bullion;
        public static ThingDef BoR_BankUplink;
        public static JobDef BoR_UseBankUplink;
        public static DutyDef BoR_BankHeist;
        public static RaidStrategyDef BoR_BankHeistStrategy;
        public static IncidentDef BoR_BankHeistRaid;

        static BoR_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(BoR_DefOf));
        }
    }
}
