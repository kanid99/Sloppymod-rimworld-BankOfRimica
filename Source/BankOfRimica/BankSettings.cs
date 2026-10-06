using UnityEngine;
using Verse;

namespace BankOfRimica
{
    public class BankSettings : ModSettings
    {
        // Savings
        public float depositInterestPerQuadrum = 0.03f;

        // Loans
        public float loanInterestPerQuadrum = 0.10f;
        public float creditLimitWealthFraction = 0.20f;
        public float latePenalty = 0.10f;
        public float defaultPenalty = 0.25f;
        public float gracePeriodDays = 3f;

        // Bounty contract (default)
        public float bountyHuntIntervalDays = 7f;
        public float bountyEscalationPerHunt = 0.15f;

        // Vault custody
        public float custodySilverReward = 0.01f;
        public float custodyCreditReward = 0.05f;
        public float shopPriceMultiplier = 2.0f;
        public float heistEscalation = 0.25f;
        public float custodyHoldingWealthFraction = 0.50f;
        public float custodyExtraRaidMtbDays = 6f;
        public float heistPointsMultiplier = 2.0f;
        public float theftPenaltyMultiplier = 1.25f;
        public bool useGauntletForHeists = true;
        public float gauntletChance = 0.15f;
        public float gauntletChanceMillion = 0.75f;

        // Debt collection
        public float collectionCommission = 0.25f;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref depositInterestPerQuadrum, "depositInterestPerQuadrum", 0.03f);
            Scribe_Values.Look(ref loanInterestPerQuadrum, "loanInterestPerQuadrum", 0.10f);
            Scribe_Values.Look(ref creditLimitWealthFraction, "creditLimitWealthFraction", 0.20f);
            Scribe_Values.Look(ref latePenalty, "latePenalty", 0.10f);
            Scribe_Values.Look(ref defaultPenalty, "defaultPenalty", 0.25f);
            Scribe_Values.Look(ref gracePeriodDays, "gracePeriodDays", 3f);
            Scribe_Values.Look(ref bountyHuntIntervalDays, "bountyHuntIntervalDays", 7f);
            Scribe_Values.Look(ref bountyEscalationPerHunt, "bountyEscalationPerHunt", 0.15f);
            Scribe_Values.Look(ref custodySilverReward, "custodySilverReward", 0.01f);
            Scribe_Values.Look(ref custodyCreditReward, "custodyCreditReward", 0.05f);
            Scribe_Values.Look(ref shopPriceMultiplier, "shopPriceMultiplier", 2.0f);
            Scribe_Values.Look(ref heistEscalation, "heistEscalation", 0.25f);
            Scribe_Values.Look(ref custodyHoldingWealthFraction, "custodyHoldingWealthFraction", 0.50f);
            Scribe_Values.Look(ref custodyExtraRaidMtbDays, "custodyExtraRaidMtbDays", 6f);
            Scribe_Values.Look(ref heistPointsMultiplier, "heistPointsMultiplier", 2.0f);
            Scribe_Values.Look(ref theftPenaltyMultiplier, "theftPenaltyMultiplier", 1.25f);
            Scribe_Values.Look(ref useGauntletForHeists, "useGauntletForHeists", true);
            Scribe_Values.Look(ref gauntletChance, "gauntletChance", 0.15f);
            Scribe_Values.Look(ref gauntletChanceMillion, "gauntletChanceMillion", 0.75f);
            Scribe_Values.Look(ref collectionCommission, "collectionCommission", 0.25f);
        }

        private Vector2 scroll;

        public void DoWindowContents(Rect inRect)
        {
            var view = new Rect(0f, 0f, inRect.width - 20f, 1100f);
            Widgets.BeginScrollView(inRect, ref scroll, view);
            var l = new Listing_Standard();
            l.Begin(view);
            l.Label("Savings");
            Slider(l, "Deposit interest per quadrum", ref depositInterestPerQuadrum, 0f, 0.25f, true);
            l.GapLine();
            l.Label("Loans");
            Slider(l, "Loan interest per quadrum", ref loanInterestPerQuadrum, 0f, 0.5f, true);
            Slider(l, "Credit limit (fraction of colony wealth)", ref creditLimitWealthFraction, 0.05f, 1f, true);
            Slider(l, "Late fee", ref latePenalty, 0f, 0.5f, true);
            Slider(l, "Default penalty", ref defaultPenalty, 0f, 1f, true);
            Slider(l, "Grace period (days)", ref gracePeriodDays, 0f, 10f, false);
            l.GapLine();
            l.Label("Bounty contract");
            Slider(l, "Days between bounty hunter raids", ref bountyHuntIntervalDays, 2f, 30f, false);
            Slider(l, "Bounty hunter escalation per raid", ref bountyEscalationPerHunt, 0f, 0.5f, true);
            l.GapLine();
            l.Label("Vault custody");
            Slider(l, "Custody reward in silver (share of holding per quadrum)", ref custodySilverReward, 0f, 0.2f, true);
            Slider(l, "Custody reward as store credit (share of holding per quadrum)", ref custodyCreditReward, 0f, 0.5f, true);
            Slider(l, "Commodity shop price multiplier", ref shopPriceMultiplier, 0.5f, 3f, false);
            Slider(l, "Holding size (fraction of colony wealth)", ref custodyHoldingWealthFraction, 0.1f, 2f, true);
            Slider(l, "Extra raid MTB during custody (days)", ref custodyExtraRaidMtbDays, 1f, 30f, false);
            Slider(l, "Bank heist raid strength multiplier", ref heistPointsMultiplier, 1f, 5f, false);
            Slider(l, "Each later heist in a contract is stronger by", ref heistEscalation, 0f, 1f, true);
            Slider(l, "Penalty per silver of stolen bullion", ref theftPenaltyMultiplier, 1f, 3f, false);
            l.CheckboxLabeled("Bank heists arrive on gauntlet ships (needs Vanilla Factions Expanded - Pirates)" +
                              (GauntletCompat.Installed ? "" : " — not installed"), ref useGauntletForHeists);
            Slider(l, "Chance a bank heist uses gauntlet ships", ref gauntletChance, 0f, 1f, true);
            Slider(l, "...when holding 1,000,000+ silver for the bank", ref gauntletChanceMillion, 0f, 1f, true);
            l.GapLine();
            l.Label("Debt collection");
            Slider(l, "Collector's commission", ref collectionCommission, 0.05f, 0.75f, true);
            l.End();
            Widgets.EndScrollView();
        }

        private static void Slider(Listing_Standard l, string label, ref float value, float min, float max, bool percent)
        {
            string shown = percent ? value.ToStringPercent() : value.ToString("0.##");
            l.Label(label + ": " + shown);
            value = l.Slider(value, min, max);
        }
    }

    public class BankOfRimicaMod : Mod
    {
        public static BankSettings Settings;

        public BankOfRimicaMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<BankSettings>();
        }

        public override string SettingsCategory() => "Bank of Rimica";

        public override void DoSettingsWindowContents(Rect inRect) => Settings.DoWindowContents(inRect);
    }
}
