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
        public static ThingDef BoR_BankPallet;
        public static ThingDef BoR_BankShuttleIncoming;
        public static ThingDef BoR_BankShuttleLanded;
        public static ThingDef BoR_BankShuttleLeaving;
        public static ThingDef BoR_HeistShuttleIncoming;
        public static ThingDef BoR_HeistShuttleLanded;
        public static ThingDef BoR_GetawayLeaving;
        public static JobDef BoR_UseBankUplink;
        public static JobDef BoR_StealBullion;
        public static JobDef BoR_BoardGetaway;
        public static DutyDef BoR_BankHeist;
        public static DutyDef BoR_HeistCoverBoard;
        public static RaidStrategyDef BoR_BankHeistStrategy;
        public static IncidentDef BoR_BankHeistRaid;

        static BoR_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(BoR_DefOf));
        }
    }
}
