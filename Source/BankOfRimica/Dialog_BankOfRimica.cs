using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace BankOfRimica
{
    public class Dialog_BankOfRimica : Window
    {
        private enum Tab { Account, Loans, Custody, Collection }

        private readonly Pawn negotiator;
        private readonly Map map;
        private Tab tab = Tab.Account;

        private int amount = 500;
        private string amountBuffer;
        private int loanDays = 30;

        public override Vector2 InitialSize => new Vector2(720f, 620f);

        public Dialog_BankOfRimica(Pawn negotiator, Thing uplink)
        {
            this.negotiator = negotiator;
            map = uplink.Map;
            forcePause = true;
            absorbInputAroundWindow = true;
            doCloseX = true;
            closeOnClickedOutside = false;
            Bank.EnsureOffers(map);
        }

        private static BankComponent Bank => BankUtility.Bank;

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 36f), BankUtility.BankName);
            Text.Font = GameFont.Small;
            GUI.color = Color.gray;
            Widgets.Label(new Rect(inRect.x, inRect.y + 34f, inRect.width, 24f),
                $"{(negotiator != null ? negotiator.LabelShort + " on the line.   " : "")}Silver near trade beacons: {BankUtility.Money(BankUtility.SilverInBeaconRange(map))}");
            GUI.color = Color.white;

            Rect body = new Rect(inRect.x, inRect.y + 96f, inRect.width, inRect.height - 96f);

            if (Bank.bountyActive)
            {
                DrawBounty(body);
                return;
            }

            var tabs = new List<TabRecord>
            {
                new TabRecord("Account", () => tab = Tab.Account, tab == Tab.Account),
                new TabRecord("Loans", () => tab = Tab.Loans, tab == Tab.Loans),
                new TabRecord("Vault custody", () => tab = Tab.Custody, tab == Tab.Custody),
                new TabRecord("Debt collection", () => tab = Tab.Collection, tab == Tab.Collection),
            };
            Widgets.DrawMenuSection(body);
            TabDrawer.DrawTabs(body, tabs);
            Rect inner = body.ContractedBy(14f);

            switch (tab)
            {
                case Tab.Account: DrawAccount(inner); break;
                case Tab.Loans: DrawLoans(inner); break;
                case Tab.Custody: DrawCustody(inner); break;
                case Tab.Collection: DrawCollection(inner); break;
            }
        }

        // ------------------------------------------------------------- widgets

        private void AmountField(Listing_Standard l, int max)
        {
            Rect row = l.GetRect(30f);
            Widgets.Label(row.LeftPart(0.25f), "Amount:");
            Rect field = new Rect(row.x + row.width * 0.25f, row.y, 120f, 28f);
            int clamped = Mathf.Clamp(amount, 0, Mathf.Max(0, max));
            if (clamped != amount) { amount = clamped; amountBuffer = null; }
            Widgets.TextFieldNumeric(field, ref amount, ref amountBuffer, 0, Mathf.Max(0, max));
            float x = field.xMax + 10f;
            foreach (int step in new[] { 100, 1000, 10000 })
            {
                if (Widgets.ButtonText(new Rect(x, row.y, 60f, 28f), "+" + step)) { amount = Mathf.Min(amount + step, max); amountBuffer = null; }
                x += 64f;
            }
            if (Widgets.ButtonText(new Rect(x, row.y, 60f, 28f), "Max")) { amount = Mathf.Max(0, max); amountBuffer = null; }
            x += 64f;
            if (Widgets.ButtonText(new Rect(x, row.y, 60f, 28f), "Clear")) { amount = 0; amountBuffer = null; }
            l.Gap(6f);
        }

        private static bool Button(Listing_Standard l, string label, bool enabled, string disabledReason = null)
        {
            Rect r = l.GetRect(32f).LeftPart(0.6f);
            if (!enabled)
            {
                GUI.color = Color.gray;
                Widgets.ButtonText(r, label + (disabledReason.NullOrEmpty() ? "" : " (" + disabledReason + ")"), active: false);
                GUI.color = Color.white;
                l.Gap(4f);
                return false;
            }
            bool clicked = Widgets.ButtonText(r, label);
            l.Gap(4f);
            return clicked;
        }

        // ------------------------------------------------------------- tabs

        private void DrawAccount(Rect rect)
        {
            var l = new Listing_Standard();
            l.Begin(rect);
            l.Label($"Savings balance: {BankUtility.Money(Bank.balance)}");
            l.Label($"Interest: {BankUtility.Settings.depositInterestPerQuadrum.ToStringPercent()} per quadrum, paid daily. Earned so far: {BankUtility.Money(Bank.lifetimeInterest)}");
            l.Label($"Credit rating: {Bank.creditRating:0.00}");
            l.GapLine();
            l.Label("Deposits are launched from silver within range of a powered orbital trade beacon. Withdrawals arrive by drop pod.");
            int beacon = BankUtility.SilverInBeaconRange(map);
            int max = Mathf.Max(beacon, Mathf.FloorToInt(Bank.balance));
            AmountField(l, max);
            if (Button(l, $"Deposit {amount} silver", amount > 0 && amount <= beacon, "not enough silver near beacons"))
                Bank.TryDeposit(map, amount);
            if (Button(l, $"Withdraw {amount} silver", amount > 0 && amount <= Mathf.FloorToInt(Bank.balance), "insufficient balance"))
                Bank.TryWithdraw(map, amount);
            l.End();
        }

        private void DrawLoans(Rect rect)
        {
            var l = new Listing_Standard();
            l.Begin(rect);
            Loan loan = Bank.loan;
            if (loan != null)
            {
                int left = loan.dueTick - Find.TickManager.TicksGame;
                l.Label($"{loan.reason}: {BankUtility.Money(loan.owed)} outstanding.");
                l.Label(left > 0
                    ? $"Due in {BankUtility.Days(left)}."
                    : $"OVERDUE. Bounty contract in {BankUtility.Days(loan.dueTick + Mathf.RoundToInt(BankUtility.Settings.gracePeriodDays * GenDate.TicksPerDay) - Find.TickManager.TicksGame)} unless paid.");
                l.GapLine();
                int beacon = BankUtility.SilverInBeaconRange(map);
                AmountField(l, loan.Owed);
                if (Button(l, $"Pay {amount} from beacon silver", amount > 0 && amount <= beacon, "not enough silver near beacons"))
                    Bank.TryPayLoan(map, amount, false);
                if (Button(l, $"Pay {amount} from savings", amount > 0 && amount <= Mathf.FloorToInt(Bank.balance), "insufficient balance"))
                    Bank.TryPayLoan(map, amount, true);
                l.GapLine();
                GUI.color = new Color(1f, 0.5f, 0.5f);
                if (Button(l, "Refuse to pay", true))
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        $"Refuse to repay the {BankUtility.BankName}? Your savings will be seized and a permanent bounty contract will be placed on your colony until the debt is paid or the colony is destroyed.",
                        () => Bank.Default("refused to repay its loan"), destructive: true));
                }
                GUI.color = Color.white;
                l.End();
                return;
            }

            int limit = Bank.CreditLimit(map);
            l.Label($"Credit limit: {BankUtility.Money(limit)} (based on colony wealth and your credit rating of {Bank.creditRating:0.00}).");
            l.Label("Loan silver is delivered by drop pod. Defaulting means seized deposits and a bounty contract on your colony.");
            l.GapLine();
            AmountField(l, limit);
            Rect termRow = l.GetRect(30f);
            Widgets.Label(termRow.LeftPart(0.25f), "Term:");
            float x = termRow.x + termRow.width * 0.25f;
            foreach (int days in BankComponent.TermDays)
            {
                Rect r = new Rect(x, termRow.y, 140f, 28f);
                if (Widgets.RadioButtonLabeled(r, $"{days} days ({BankComponent.LoanInterestFor(days).ToStringPercent()})", loanDays == days)) loanDays = days;
                x += 150f;
            }
            l.Gap(6f);
            float repay = amount * (1f + BankComponent.LoanInterestFor(loanDays));
            l.Label($"You will owe {BankUtility.Money(repay)} in {loanDays} days.");
            if (Button(l, $"Borrow {amount} silver", amount >= 100 && amount <= limit, "invalid amount"))
                Bank.TryTakeLoan(map, amount, loanDays);
            l.End();
        }

        private void DrawCustody(Rect rect)
        {
            var l = new Listing_Standard();
            l.Begin(rect);
            CustodyContract active = Bank.custody;
            if (active != null)
            {
                l.Label($"Active custody contract: {active.bullionCount} bars of bullion ({BankUtility.Money(active.HoldingValue)}).");
                l.Label($"Ends in {BankUtility.Days(active.endTick - Find.TickManager.TicksGame)}. Fee on completion: {BankUtility.Money(active.fee)}.");
                l.Label($"Bullion currently held by your colony: {BankUtility.CountPlayerBullion(false)} bars.");
                l.Label($"Missing bars are charged at {BankUtility.Settings.theftPenaltyMultiplier:0.##}x value. Bank heists so far: {active.heistsLaunched}.");
                l.End();
                return;
            }

            l.Label("The bank needs somewhere off its books to hold reserves. Store its bullion for a while and get paid for it.");
            GUI.color = new Color(1f, 0.75f, 0.4f);
            l.Label("Warning: the bullion counts toward your colony wealth, attracts many more raids, and will draw at least one massive bank heist. Every missing bar is charged against you.");
            GUI.color = Color.white;
            l.GapLine();
            foreach (CustodyContract offer in Bank.custodyOffers.ToArray())
            {
                l.Label($"{offer.durationDays} days — hold {offer.bullionCount} bars ({BankUtility.Money(offer.HoldingValue)}), fee {BankUtility.Money(offer.fee)}, heists expected: {Mathf.Max(1, offer.durationDays / GenDate.DaysPerQuadrum)}");
                if (Button(l, "Accept this contract", true))
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        $"Accept {offer.bullionCount} bars of bank bullion for {offer.durationDays} days? Expect a bank heist.",
                        () => Bank.TryAcceptCustody(map, offer)));
                }
                l.Gap(6f);
            }
            if (Bank.custodyOffers.Count == 0) l.Label("No custody contracts on offer right now. New offers arrive each quadrum.");
            l.End();
        }

        private void DrawCollection(Rect rect)
        {
            var l = new Listing_Standard();
            l.Begin(rect);
            CollectionContract active = Bank.collection;
            if (active != null)
            {
                l.Label($"Active contract: collect {BankUtility.Money(active.debt)} from {active.target?.LabelCap ?? "(gone)"}" +
                        (active.target != null ? $" ({active.target.Faction.Name})." : "."));
                l.Label($"Your commission: {BankUtility.Money(active.Commission)}. Time left: {BankUtility.Days(active.deadlineTick - Find.TickManager.TicksGame)}.");
                l.Label(active.attempted
                    ? "They refused to pay. Destroy the settlement to seize its assets."
                    : "Send a caravan to the settlement and use 'Collect debt'.");
                if (active.target != null && Button(l, "Show on world map", true))
                {
                    Close();
                    CameraJumper.TryJumpAndSelect(active.target);
                }
                if (Button(l, "Abandon contract", true)) Bank.CancelCollection();
                l.End();
                return;
            }

            l.Label("These settlements have defaulted on the bank. Collect for us and keep a commission.");
            l.GapLine();
            foreach (CollectionContract offer in Bank.collectionOffers.ToArray())
            {
                if (offer.TargetGone) continue;
                int dist = Mathf.RoundToInt(Find.WorldGrid.ApproxDistanceInTiles(offer.target.Tile, map.Tile));
                l.Label($"{offer.target.LabelCap} ({offer.target.Faction.Name}, {offer.target.Faction.PlayerRelationKind.GetLabel()}) — {dist} tiles away");
                l.Label($"   Debt: {BankUtility.Money(offer.debt)}   Commission: {BankUtility.Money(offer.Commission)}   Deadline: {offer.durationDays} days");
                if (Button(l, "Take the contract", true)) Bank.TryAcceptCollection(offer);
                l.Gap(6f);
            }
            if (Bank.collectionOffers.Count == 0) l.Label("No collection contracts available right now.");
            l.End();
        }

        private void DrawBounty(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            var l = new Listing_Standard();
            l.Begin(rect.ContractedBy(14f));
            GUI.color = new Color(1f, 0.4f, 0.4f);
            Text.Font = GameFont.Medium;
            l.Label("BOUNTY CONTRACT ACTIVE");
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
            l.Label($"You defaulted on your debt. The bank has hired bounty hunters, who will keep coming until you pay or your colony is destroyed.");
            l.Label($"Amount owed: {BankUtility.Money(Bank.bountyOwed)}. Bounty hunter bands sent so far: {Bank.huntCount}.");
            l.Label("All other banking services are suspended.");
            l.GapLine();
            int beacon = BankUtility.SilverInBeaconRange(map);
            AmountField(l, Bank.BountyOwed);
            if (Button(l, $"Pay {amount} from beacon silver", amount > 0 && amount <= beacon, "not enough silver near beacons"))
                Bank.TryPayBounty(map, amount, false);
            if (Button(l, $"Pay {amount} from savings", amount > 0 && amount <= Mathf.FloorToInt(Bank.balance), "insufficient balance"))
                Bank.TryPayBounty(map, amount, true);
            l.End();
        }
    }
}
