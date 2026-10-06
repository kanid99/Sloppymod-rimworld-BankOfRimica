using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace BankOfRimica
{
    public class Dialog_BankOfRimica : Window
    {
        private enum Tab { Account, Loans, Custody, Collection, Shop }

        private readonly Pawn negotiator;
        private readonly Map map;
        private Tab tab = Tab.Account;

        private int amount = 500;
        private string amountBuffer;
        private int loanDays = 30;

        public override Vector2 InitialSize => new Vector2(860f, 700f);

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
                new TabRecord("Commodity shop", () => tab = Tab.Shop, tab == Tab.Shop),
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
                case Tab.Shop: DrawShop(inner); break;
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

        private void DrawOpinion(Listing_Standard l)
        {
            float op = Bank.opinion;
            Rect r = l.GetRect(24f);
            Widgets.Label(r.LeftPart(0.45f), $"Bank opinion: {op:0} ({BankComponent.OpinionLabel(op)})");
            Rect bar = new Rect(r.x + r.width * 0.45f, r.y + 4f, r.width * 0.3f, 16f);
            Widgets.FillableBar(bar, (op - BankComponent.OpinionMin) / (BankComponent.OpinionMax - BankComponent.OpinionMin));
            Widgets.Label(new Rect(bar.xMax + 8f, r.y, r.xMax - bar.xMax - 8f, r.height),
                $"loans x{Bank.LoanRateFactor:0.00}, deposits x{Bank.DepositRateFactor:0.00}");
            TooltipHandler.TipRegion(r, "The bank's opinion of your colony rises with successful missions: loans repaid on time, debts collected, custody contracts with every bar returned. " +
                                        "Failures and defaults lower it. Higher opinion means cheaper loans and better interest on savings.");
        }

        // ------------------------------------------------------------- tabs

        private void DrawAccount(Rect rect)
        {
            var l = new Listing_Standard();
            l.Begin(rect);
            l.Label($"Savings balance: {BankUtility.Money(Bank.balance)}");
            l.Label($"Interest: {Bank.DepositRate.ToStringPercent("0.##")} per quadrum, paid daily. Earned so far: {BankUtility.Money(Bank.lifetimeInterest)}");
            l.Label($"Credit rating: {Bank.creditRating:0.00}");
            DrawOpinion(l);
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
            DrawOpinion(l);
            l.Label("Loan silver is delivered by drop pod. Defaulting means seized deposits and a bounty contract on your colony.");
            l.GapLine();
            AmountField(l, limit);
            Rect termRow = l.GetRect(30f);
            Widgets.Label(termRow.LeftPart(0.25f), "Term:");
            float x = termRow.x + termRow.width * 0.25f;
            foreach (int days in BankComponent.TermDays)
            {
                Rect r = new Rect(x, termRow.y, 140f, 28f);
                if (Widgets.RadioButtonLabeled(r, $"{days} days ({Bank.LoanInterestFor(days).ToStringPercent()})", loanDays == days)) loanDays = days;
                x += 150f;
            }
            l.Gap(6f);
            float repay = amount * (1f + Bank.LoanInterestFor(loanDays));
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
                l.Label($"Active custody contract: {active.bullionCount} silver bars ({BankUtility.Money(active.HoldingValue)}).");
                l.Label($"Ends in {BankUtility.Days(active.endTick - Find.TickManager.TicksGame)}. Reward on completion: {active.RewardLabel}.");
                l.Label($"Silver bars currently held by your colony: {BankUtility.CountPlayerBullion(false)}." + (active.collectionDispatched ? " The bank shuttle is on its way to collect them." : ""));
                l.Label($"Missing bars are charged at {BankUtility.Settings.theftPenaltyMultiplier:0.##}x value. Bank heists so far: {active.heistsLaunched}.");
                l.End();
                return;
            }

            l.Label("The bank needs somewhere off its books to hold reserves. Host a crate of its silver bars (10,000 silver each) and get paid for it. You choose where the crate goes; a bank shuttle delivers it and collects it at the end.");
            GUI.color = new Color(1f, 0.75f, 0.4f);
            l.Label("Warning: the silver counts toward your colony wealth, attracts many more raids, and will draw at least one massive bank heist. Every missing bar is charged against you.");
            GUI.color = Color.white;
            l.GapLine();
            foreach (CustodyContract offer in Bank.custodyOffers.ToArray())
            {
                l.Label($"{offer.durationDays} days — hold {offer.bullionCount} silver bars ({BankUtility.Money(offer.HoldingValue)}), heists expected: {Mathf.Max(1, offer.durationDays / GenDate.DaysPerQuadrum)}, each stronger than the last");
                foreach (bool credit in new[] { false, true })
                {
                    int reward = credit ? offer.CreditReward : offer.SilverReward;
                    string how = credit ? $"{BankUtility.Money(reward)} in store credit" : BankUtility.Money(reward);
                    if (Button(l, $"Accept — paid {how}", true))
                    {
                        Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                            $"Host {offer.bullionCount} silver bars for {offer.durationDays} days for {how}? " +
                            (credit ? "Store credit can only be spent at the Rimica Commodity Exchange and is never paid out as silver. " : "") +
                            "You'll choose where the bank's crate goes. Expect a bank heist.",
                            () => { Close(); Bank.BeginCustodyPlacement(map, offer, credit); }));
                    }
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

        // ------------------------------------------------------------- commodity shop

        private const float ShopRowHeight = 30f;
        private string shopSearch = "";
        private string shopCategory;
        private Vector2 shopScroll;
        private readonly Dictionary<CartKey, int> cart = new Dictionary<CartKey, int>();
        private readonly Dictionary<ShopEntry, QualityCategory> chosenQuality = new Dictionary<ShopEntry, QualityCategory>();

        private float CartTotal()
        {
            float total = 0f;
            foreach (KeyValuePair<CartKey, int> kv in cart) total += kv.Key.Price * kv.Value;
            return total;
        }

        private QualityCategory QualityFor(ShopEntry e) =>
            chosenQuality.TryGetValue(e, out QualityCategory q) ? q : QualityCategory.Normal;

        private void DrawShop(Rect rect)
        {
            float credit = Bank.storeCredit;
            float total = CartTotal();

            // header: credit, search, category
            Rect head = new Rect(rect.x, rect.y, rect.width, 30f);
            Widgets.Label(head.LeftPart(0.4f), $"Store credit: {BankUtility.Money(credit)}");
            Rect search = new Rect(head.x + head.width * 0.4f, head.y, head.width * 0.32f, 28f);
            shopSearch = Widgets.TextField(search, shopSearch);
            if (shopSearch.NullOrEmpty())
            {
                GUI.color = Color.gray;
                Widgets.Label(search.ContractedBy(4f, 2f), "Search...");
                GUI.color = Color.white;
            }
            Rect catRect = new Rect(search.xMax + 8f, head.y, head.xMax - search.xMax - 8f, 28f);
            if (Widgets.ButtonText(catRect, shopCategory ?? "All categories"))
            {
                var options = new List<FloatMenuOption> { new FloatMenuOption("All categories", () => shopCategory = null) };
                foreach (string c in CommodityShop.Categories)
                {
                    string cat = c;
                    options.Add(new FloatMenuOption(cat, () => shopCategory = cat));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            GUI.color = Color.gray;
            Widgets.Label(new Rect(rect.x, rect.y + 32f, rect.width, 24f),
                "Earned by taking custody contracts paid in credit. Orders arrive by drop pod. Unspent credit stays on account; it is never paid out as silver.");
            GUI.color = Color.white;

            // catalogue (only visible rows are drawn)
            List<ShopEntry> entries = Filtered();
            Rect outRect = new Rect(rect.x, rect.y + 60f, rect.width, rect.height - 60f - 44f);
            Rect view = new Rect(0f, 0f, outRect.width - 16f, entries.Count * ShopRowHeight);
            Widgets.BeginScrollView(outRect, ref shopScroll, view);
            int first = Mathf.Max(0, Mathf.FloorToInt(shopScroll.y / ShopRowHeight));
            int last = Mathf.Min(entries.Count - 1, first + Mathf.CeilToInt(outRect.height / ShopRowHeight) + 1);
            for (int i = first; i <= last; i++)
            {
                DrawShopRow(new Rect(0f, i * ShopRowHeight, view.width, ShopRowHeight), entries[i], i);
            }
            Widgets.EndScrollView();

            // footer: cart total and order button
            Rect foot = new Rect(rect.x, rect.yMax - 38f, rect.width, 34f);
            int items = 0;
            foreach (int n in cart.Values) items += n;
            Rect summary = foot.LeftPart(0.5f);
            Widgets.Label(summary, $"Cart: {items} items, {BankUtility.Money(total)}" + (total > credit ? "  (not enough credit)" : ""));
            if (cart.Count > 0)
            {
                var lines = new System.Text.StringBuilder();
                foreach (KeyValuePair<CartKey, int> kv in cart)
                {
                    string q = kv.Key.entry.HasQuality ? $" ({kv.Key.quality.GetLabel()})" : "";
                    lines.AppendLine($"{kv.Value}x {kv.Key.entry.label}{q} — {BankUtility.Money(kv.Key.Price * kv.Value)}");
                }
                TooltipHandler.TipRegion(summary, lines.ToString());
            }
            if (Widgets.ButtonText(new Rect(foot.xMax - 300f, foot.y, 140f, 32f), "Clear cart")) cart.Clear();
            bool canOrder = items > 0 && total <= credit + 0.01f;
            if (Widgets.ButtonText(new Rect(foot.xMax - 150f, foot.y, 150f, 32f), "Place order", active: canOrder) && canOrder)
            {
                if (Bank.TryPlaceOrder(map, cart)) cart.Clear();
            }
        }

        private List<ShopEntry> Filtered()
        {
            var result = new List<ShopEntry>();
            foreach (ShopEntry e in CommodityShop.Catalog)
            {
                if (shopCategory != null && e.category != shopCategory) continue;
                if (!shopSearch.NullOrEmpty() && e.label.IndexOf(shopSearch, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                result.Add(e);
            }
            return result;
        }

        private void DrawShopRow(Rect row, ShopEntry e, int index)
        {
            if (index % 2 == 1) Widgets.DrawLightHighlight(row);
            Widgets.DrawHighlightIfMouseover(row);
            Widgets.InfoCardButton(row.x, row.y + 3f, e.def, e.stuff);
            Widgets.ThingIcon(new Rect(row.x + 28f, row.y + 2f, 26f, 26f), e.def, e.stuff);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(row.x + 60f, row.y, row.width * 0.30f, row.height), e.label);
            GUI.color = Color.gray;
            Widgets.Label(new Rect(row.x + 60f + row.width * 0.30f, row.y, 100f, row.height), e.category);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            // quality picker for gear with quality
            var key = new CartKey(e, QualityFor(e));
            Rect qRect = new Rect(row.xMax - 410f, row.y + 2f, 110f, row.height - 4f);
            if (e.HasQuality)
            {
                if (Widgets.ButtonText(qRect, key.quality.GetLabel().CapitalizeFirst()))
                {
                    var options = new List<FloatMenuOption>();
                    foreach (QualityCategory q in QualityUtility.AllQualityCategories)
                    {
                        QualityCategory chosen = q;
                        options.Add(new FloatMenuOption($"{q.GetLabel().CapitalizeFirst()} — {BankUtility.Money(e.PriceAt(q))}", () => chosenQuality[e] = chosen));
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }
            }

            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(row.xMax - 290f, row.y, 100f, row.height), BankUtility.Money(key.Price).Replace(" silver", ""));
            cart.TryGetValue(key, out int count);
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(new Rect(row.xMax - 120f, row.y, 50f, row.height), count.ToString());
            Text.Anchor = TextAnchor.UpperLeft;
            int step = Event.current.shift ? 10 : Event.current.control ? 100 : 1;
            if (Widgets.ButtonText(new Rect(row.xMax - 170f, row.y + 2f, 46f, row.height - 4f), "-")) SetCart(key, count - step);
            if (Widgets.ButtonText(new Rect(row.xMax - 66f, row.y + 2f, 46f, row.height - 4f), "+")) SetCart(key, count + step);
            string qualityNote = e.HasQuality ? $" at {key.quality.GetLabel()} quality" : "";
            TooltipHandler.TipRegion(row, $"{e.label}{qualityNote}: {BankUtility.Money(key.Price)} each.\nShift-click for 10, Ctrl-click for 100." +
                                          (e.HasQuality ? "\nBetter quality costs steeply more." : ""));
        }

        private void SetCart(CartKey key, int count)
        {
            if (count <= 0) cart.Remove(key);
            else cart[key] = count;
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
