using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace BankOfRimica
{
    /// <summary>Money the colony owes the bank on a fixed schedule.</summary>
    public class Loan : IExposable
    {
        public int principal;
        public float owed;
        public int issuedTick;
        public int dueTick;
        public bool lateFeeApplied;
        public string reason = "Loan";

        public int Owed => Mathf.CeilToInt(owed);

        public void ExposeData()
        {
            Scribe_Values.Look(ref principal, "principal");
            Scribe_Values.Look(ref owed, "owed");
            Scribe_Values.Look(ref issuedTick, "issuedTick");
            Scribe_Values.Look(ref dueTick, "dueTick");
            Scribe_Values.Look(ref lateFeeApplied, "lateFeeApplied");
            Scribe_Values.Look(ref reason, "reason", "Loan");
        }
    }

    /// <summary>The colony stores a bank holding (physical bullion) for a period, for a fee.</summary>
    public class CustodyContract : IExposable
    {
        public int durationDays;
        public int bullionCount;
        public int fee;

        // Set once accepted.
        public int startTick = -1;
        public int endTick = -1;
        public int mapId = -1;
        public System.Collections.Generic.List<int> heistTicks = new System.Collections.Generic.List<int>();
        public int heistsLaunched;

        public int HoldingValue => bullionCount * BankUtility.BullionUnitValue;

        public Map Map => Find.Maps.Find(m => m.uniqueID == mapId);

        public void ExposeData()
        {
            Scribe_Values.Look(ref durationDays, "durationDays");
            Scribe_Values.Look(ref bullionCount, "bullionCount");
            Scribe_Values.Look(ref fee, "fee");
            Scribe_Values.Look(ref startTick, "startTick", -1);
            Scribe_Values.Look(ref endTick, "endTick", -1);
            Scribe_Values.Look(ref mapId, "mapId", -1);
            Scribe_Collections.Look(ref heistTicks, "heistTicks", LookMode.Value);
            Scribe_Values.Look(ref heistsLaunched, "heistsLaunched");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && heistTicks == null)
                heistTicks = new System.Collections.Generic.List<int>();
        }
    }

    /// <summary>A defaulted settlement the bank wants squeezed for money.</summary>
    public class CollectionContract : IExposable
    {
        public Settlement target;
        public int debt;
        public int durationDays = 30;
        public int deadlineTick = -1;
        public bool attempted;

        public int Commission => Mathf.RoundToInt(debt * BankUtility.Settings.collectionCommission);

        public bool TargetGone => target == null || target.Destroyed;

        public void ExposeData()
        {
            Scribe_References.Look(ref target, "target");
            Scribe_Values.Look(ref debt, "debt");
            Scribe_Values.Look(ref durationDays, "durationDays", 30);
            Scribe_Values.Look(ref deadlineTick, "deadlineTick", -1);
            Scribe_Values.Look(ref attempted, "attempted");
        }
    }
}
