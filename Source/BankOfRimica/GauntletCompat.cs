using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace BankOfRimica
{
    /// <summary>
    /// Optional integration with Vanilla Factions Expanded - Pirates: bank heists crash in on gauntlet ships.
    /// Gauntlet ships hold their pawns for a few seconds, then put them in a fresh assault lord, so the heist
    /// pawns are tracked here and moved back into a bank heist lord once they climb out.
    /// </summary>
    public static class GauntletCompat
    {
        public const string ArrivalModeDefName = "VFEP_GauntletDrop";

        private static bool looked;
        private static PawnsArrivalModeDef arrivalMode;
        private static FieldInfo vfeSettingsField;
        private static FieldInfo vfeDisableField;

        private class HeistGroup
        {
            public List<Pawn> pawns;
            public Map map;
            public Faction faction;
            public Lord lord;
            public int expireTick;
        }

        // Transient: gauntlet ships don't save their passengers either, so there's nothing to restore on load.
        private static readonly List<HeistGroup> pending = new List<HeistGroup>();

        public static PawnsArrivalModeDef ArrivalMode
        {
            get
            {
                if (!looked)
                {
                    looked = true;
                    arrivalMode = DefDatabase<PawnsArrivalModeDef>.GetNamedSilentFail(ArrivalModeDefName);
                    System.Type modType = GenTypes.GetTypeInAnyAssembly("VFEPirates.VFEPiratesMod");
                    vfeSettingsField = modType?.GetField("settings", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                    vfeDisableField = vfeSettingsField?.FieldType.GetField("disableGauntlet", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }
                return arrivalMode;
            }
        }

        public static bool Installed => ArrivalMode != null;

        /// <summary>True when VFE Pirates is loaded, our setting allows it, and VFE Pirates hasn't disabled gauntlet raids.</summary>
        public static bool ShouldUse
        {
            get
            {
                if (!BankUtility.Settings.useGauntletForHeists || ArrivalMode == null) return false;
                try
                {
                    object settings = vfeSettingsField?.GetValue(null);
                    if (settings != null && vfeDisableField != null && (bool)vfeDisableField.GetValue(settings)) return false;
                }
                catch (System.Exception e)
                {
                    Log.WarningOnce("[Bank of Rimica] Couldn't read VFE Pirates gauntlet setting: " + e.Message, 0x5B0F1CA);
                }
                return true;
            }
        }

        public static bool IsGauntlet(IncidentParms parms) => parms.raidArrivalMode != null && parms.raidArrivalMode == ArrivalMode;

        public static void Track(List<Pawn> pawns, Map map, Faction faction)
        {
            if (pawns.NullOrEmpty() || map == null) return;
            pending.Add(new HeistGroup
            {
                pawns = new List<Pawn>(pawns),
                map = map,
                faction = faction,
                expireTick = Find.TickManager.TicksGame + GenDate.TicksPerHour * 2,
            });
        }

        /// <summary>Moves heist pawns that have left their gauntlet ship into a bank heist lord.</summary>
        public static void Tick()
        {
            if (pending.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            for (int g = pending.Count - 1; g >= 0; g--)
            {
                HeistGroup group = pending[g];
                for (int i = group.pawns.Count - 1; i >= 0; i--)
                {
                    Pawn p = group.pawns[i];
                    if (p == null || p.Destroyed || p.Dead)
                    {
                        group.pawns.RemoveAt(i);
                        continue;
                    }
                    if (!p.Spawned || p.Map != group.map) continue;

                    Lord current = p.GetLord();
                    if (current?.LordJob is LordJob_BankHeist)
                    {
                        group.pawns.RemoveAt(i);
                        continue;
                    }
                    if (current != null)
                    {
                        current.RemovePawn(p);
                        if (current.ownedPawns.Count == 0) current.lordManager.RemoveLord(current);
                    }
                    if (group.lord == null || !group.map.lordManager.lords.Contains(group.lord))
                    {
                        Faction f = group.faction ?? p.Faction;
                        group.lord = LordMaker.MakeNewLord(f, new LordJob_BankHeist(f), group.map);
                    }
                    group.lord.AddPawn(p);
                    group.pawns.RemoveAt(i);
                }
                if (group.pawns.Count == 0 || now > group.expireTick) pending.RemoveAt(g);
            }
        }

        public static void Clear() => pending.Clear();
    }
}
