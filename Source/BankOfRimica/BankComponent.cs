using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace BankOfRimica
{
    /// <summary>All state of the colony's relationship with the Bank of Rimica.</summary>
    public class BankComponent : GameComponent
    {
        private const int TickInterval = 250;
        private const int OfferRefreshTicks = GenDate.TicksPerQuadrum;
        public static readonly int[] TermDays = { 15, 30, 60 };

        // Savings account
        public float balance;

        // Commodity shop credit (from custody contracts; never paid out as silver)
        public float storeCredit;
        private int lastInterestTick = -1;
        public float lifetimeInterest;

        // Credit
        public float creditRating = 1f;
        public Loan loan;

        // Default / bounty contract
        public bool bountyActive;
        public float bountyOwed;
        private int nextHuntTick = -1;
        public int huntCount;

        // Vault custody
        public List<CustodyContract> custodyOffers = new List<CustodyContract>();
        public CustodyContract custody;

        // Debt collection
        public List<CollectionContract> collectionOffers = new List<CollectionContract>();
        public CollectionContract collection;

        private int offersGeneratedTick = -999999;

        // Bank heist getaways in progress
        public List<HeistGetaway> getaways = new List<HeistGetaway>();
        private int nextGetawayId;

        public int NextGetawayId() => ++nextGetawayId;

        public BankComponent(Game game)
        {
            GauntletCompat.Clear();
        }

        private static BankSettings S => BankUtility.Settings;
        private static int Now => Find.TickManager.TicksGame;

        public int BountyOwed => Mathf.CeilToInt(bountyOwed);

        public bool CanUseServices => !bountyActive;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref balance, "balance");
            Scribe_Values.Look(ref storeCredit, "storeCredit");
            Scribe_Values.Look(ref lastInterestTick, "lastInterestTick", -1);
            Scribe_Values.Look(ref lifetimeInterest, "lifetimeInterest");
            Scribe_Values.Look(ref creditRating, "creditRating", 1f);
            Scribe_Deep.Look(ref loan, "loan");
            Scribe_Values.Look(ref bountyActive, "bountyActive");
            Scribe_Values.Look(ref bountyOwed, "bountyOwed");
            Scribe_Values.Look(ref nextHuntTick, "nextHuntTick", -1);
            Scribe_Values.Look(ref huntCount, "huntCount");
            Scribe_Collections.Look(ref custodyOffers, "custodyOffers", LookMode.Deep);
            Scribe_Deep.Look(ref custody, "custody");
            Scribe_Collections.Look(ref collectionOffers, "collectionOffers", LookMode.Deep);
            Scribe_Deep.Look(ref collection, "collection");
            Scribe_Values.Look(ref offersGeneratedTick, "offersGeneratedTick", -999999);
            Scribe_Collections.Look(ref getaways, "getaways", LookMode.Deep);
            Scribe_Values.Look(ref nextGetawayId, "nextGetawayId");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                custodyOffers ??= new List<CustodyContract>();
                collectionOffers ??= new List<CollectionContract>();
                getaways ??= new List<HeistGetaway>();
                collectionOffers.RemoveAll(c => c.TargetGone);
            }
        }

        public override void GameComponentTick()
        {
            int now = Now;
            if (now % 30 == 0)
            {
                GauntletCompat.Tick();
                GetawayUtility.Tick();
            }
            if (now % TickInterval != 0) return;

            TickInterest(now);
            TickLoan(now);
            TickBounty(now);
            TickCustody(now);
            TickCollection(now);
        }

        // ---------------------------------------------------------------- savings

        private void TickInterest(int now)
        {
            if (lastInterestTick < 0) lastInterestTick = now;
            while (now - lastInterestTick >= GenDate.TicksPerDay)
            {
                lastInterestTick += GenDate.TicksPerDay;
                if (balance <= 0f || bountyActive) continue;
                float interest = balance * S.depositInterestPerQuadrum / GenDate.DaysPerQuadrum;
                balance += interest;
                lifetimeInterest += interest;
            }
        }

        public bool TryDeposit(Map map, int amount)
        {
            if (!CanUseServices || amount <= 0) return false;
            if (!BankUtility.TryTakeSilver(map, amount)) return false;
            balance += amount;
            Messages.Message($"Deposited {BankUtility.Money(amount)} with the {BankUtility.BankName}.", MessageTypeDefOf.PositiveEvent, false);
            return true;
        }

        public bool TryWithdraw(Map map, int amount)
        {
            if (!CanUseServices || amount <= 0 || amount > Mathf.FloorToInt(balance)) return false;
            balance -= amount;
            BankUtility.DropToColony(map, ThingDefOf.Silver, amount);
            Messages.Message($"The {BankUtility.BankName} is sending {BankUtility.Money(amount)} by drop pod.", MessageTypeDefOf.PositiveEvent, false);
            return true;
        }

        // ---------------------------------------------------------------- loans

        public int CreditLimit(Map map)
        {
            if (!CanUseServices) return 0;
            float limit = BankUtility.ColonyWealth(map) * S.creditLimitWealthFraction * creditRating;
            limit = Mathf.Clamp(limit, 500f, 100000f);
            return Mathf.FloorToInt(limit / 100f) * 100;
        }

        public static float LoanInterestFor(int days) => BankUtility.Settings.loanInterestPerQuadrum * days / GenDate.DaysPerQuadrum;

        public bool TryTakeLoan(Map map, int amount, int days)
        {
            if (loan != null || !CanUseServices || amount <= 0 || amount > CreditLimit(map)) return false;
            loan = new Loan
            {
                principal = amount,
                owed = amount * (1f + LoanInterestFor(days)),
                issuedTick = Now,
                dueTick = Now + days * GenDate.TicksPerDay,
                reason = "Loan",
            };
            BankUtility.DropToColony(map, ThingDefOf.Silver, amount);
            Find.LetterStack.ReceiveLetter("Loan approved",
                $"The {BankUtility.BankName} has approved a loan of {BankUtility.Money(amount)}. The silver is on its way by drop pod.\n\n" +
                $"You must repay {BankUtility.Money(loan.owed)} within {days} days.\n\n" +
                "The bank reminds all customers that defaulting on a loan results in a standing bounty contract against the debtor colony.",
                LetterDefOf.NeutralEvent);
            return true;
        }

        /// <summary>Pays toward the loan, from beacon silver (fromAccount = false) or from savings.</summary>
        public bool TryPayLoan(Map map, int amount, bool fromAccount)
        {
            if (loan == null || amount <= 0) return false;
            amount = Mathf.Min(amount, loan.Owed);
            if (!TakePayment(map, amount, fromAccount)) return false;
            loan.owed -= amount;
            if (loan.owed < 0.5f)
            {
                bool onTime = !loan.lateFeeApplied;
                loan = null;
                if (onTime) creditRating = Mathf.Min(2f, creditRating + 0.2f);
                Find.LetterStack.ReceiveLetter("Loan repaid",
                    $"Your debt with the {BankUtility.BankName} is fully settled." +
                    (onTime ? " Prompt repayment has improved your credit rating." : ""),
                    LetterDefOf.PositiveEvent);
            }
            else
            {
                Messages.Message($"Paid {BankUtility.Money(amount)}. Remaining debt: {BankUtility.Money(loan.owed)}.", MessageTypeDefOf.NeutralEvent, false);
            }
            return true;
        }

        private bool TakePayment(Map map, int amount, bool fromAccount)
        {
            if (fromAccount)
            {
                if (amount > Mathf.FloorToInt(balance)) return false;
                balance -= amount;
                return true;
            }
            return BankUtility.TryTakeSilver(map, amount);
        }

        private void TickLoan(int now)
        {
            if (loan == null) return;
            if (now >= loan.dueTick && !loan.lateFeeApplied)
            {
                loan.lateFeeApplied = true;
                loan.owed *= 1f + S.latePenalty;
                Find.LetterStack.ReceiveLetter("Loan overdue",
                    $"Your payment to the {BankUtility.BankName} is overdue. A late fee of {S.latePenalty.ToStringPercent()} has been applied; you now owe {BankUtility.Money(loan.owed)}.\n\n" +
                    $"If the debt is not settled within {S.gracePeriodDays:0.#} days the bank will seize your deposits and place a bounty contract on your colony.",
                    LetterDefOf.NegativeEvent);
            }
            if (now >= loan.dueTick + Mathf.RoundToInt(S.gracePeriodDays * GenDate.TicksPerDay))
            {
                Default("missed the repayment deadline");
            }
        }

        /// <summary>The colony refuses to pay or misses the deadline: seize deposits, put out a bounty.</summary>
        public void Default(string why)
        {
            if (loan == null) return;
            float debt = loan.owed;
            loan = null;
            float seized = Mathf.Min(balance, debt);
            balance -= seized;
            debt -= seized;
            creditRating = 0.25f;
            CancelCollection(silent: true);

            if (debt < 0.5f)
            {
                Find.LetterStack.ReceiveLetter("Deposits seized",
                    $"Your colony {why}. The {BankUtility.BankName} has seized {BankUtility.Money(seized)} from your savings to cover the debt. Your credit rating has been slashed.",
                    LetterDefOf.NegativeEvent);
                return;
            }

            bountyActive = true;
            bountyOwed += debt * (1f + S.defaultPenalty);
            huntCount = 0;
            nextHuntTick = Now + Mathf.RoundToInt(Rand.Range(2f, 4f) * GenDate.TicksPerDay);
            Find.LetterStack.ReceiveLetter("Bounty contract issued",
                $"Your colony {why}.\n\n" +
                (seized > 0f ? $"The {BankUtility.BankName} has seized {BankUtility.Money(seized)} from your savings. " : "") +
                $"The remaining debt plus a {S.defaultPenalty.ToStringPercent()} default penalty — {BankUtility.Money(bountyOwed)} — is now the subject of an open bounty contract.\n\n" +
                "Bounty hunters will keep coming, and each band will be bigger than the last, until the colony is destroyed or the debt is paid in full. " +
                "You can still contact the bank through a bank uplink to pay what you owe.",
                LetterDefOf.ThreatBig);
        }

        // ---------------------------------------------------------------- bounty

        public bool TryPayBounty(Map map, int amount, bool fromAccount)
        {
            if (!bountyActive || amount <= 0) return false;
            amount = Mathf.Min(amount, BountyOwed);
            if (!TakePayment(map, amount, fromAccount)) return false;
            bountyOwed -= amount;
            if (bountyOwed < 0.5f)
            {
                bountyOwed = 0f;
                bountyActive = false;
                huntCount = 0;
                nextHuntTick = -1;
                creditRating = Mathf.Max(creditRating, 0.5f);
                Find.LetterStack.ReceiveLetter("Bounty contract lifted",
                    $"Your debt to the {BankUtility.BankName} is paid. The bounty contract on your colony has been withdrawn and the bank will do business with you again — on a probationary credit rating.",
                    LetterDefOf.PositiveEvent);
            }
            else
            {
                Messages.Message($"Paid {BankUtility.Money(amount)} toward the bounty. Remaining: {BankUtility.Money(bountyOwed)}.", MessageTypeDefOf.NeutralEvent, false);
            }
            return true;
        }

        private void TickBounty(int now)
        {
            if (!bountyActive || nextHuntTick < 0 || now < nextHuntTick) return;

            Map map = BankUtility.RichestHomeMap();
            if (map == null)
            {
                nextHuntTick = now + GenDate.TicksPerDay;
                return;
            }
            float mult = 1f + S.bountyEscalationPerHunt * huntCount;
            string text = $"Bounty hunters contracted by the {BankUtility.BankName} have come to collect on your unpaid debt of {BankUtility.Money(bountyOwed)}.\n\n" +
                          "They will not be the last. Pay the bank through a bank uplink to end the contract.";
            if (BankUtility.FireRaid(map, mult, "Bounty hunters", text))
            {
                huntCount++;
                nextHuntTick = now + Mathf.RoundToInt(S.bountyHuntIntervalDays * Rand.Range(0.75f, 1.25f) * GenDate.TicksPerDay);
            }
            else
            {
                nextHuntTick = now + GenDate.TicksPerDay / 2;
            }
        }

        /// <summary>Money owed after a custody shortfall: rolled into the loan, the bounty, or a new short-term loan.</summary>
        private void AddDebt(float amount, string reason)
        {
            if (amount <= 0f) return;
            if (bountyActive)
            {
                bountyOwed += amount;
                return;
            }
            if (loan != null)
            {
                loan.owed += amount;
                return;
            }
            loan = new Loan
            {
                principal = Mathf.CeilToInt(amount),
                owed = amount,
                issuedTick = Now,
                dueTick = Now + GenDate.TicksPerQuadrum,
                reason = reason,
            };
        }

        // ---------------------------------------------------------------- offers

        public void EnsureOffers(Map map)
        {
            bool stale = Now - offersGeneratedTick >= OfferRefreshTicks;
            collectionOffers.RemoveAll(c => c.TargetGone);
            if (!stale && custodyOffers.Count > 0 && collectionOffers.Count > 0) return;
            offersGeneratedTick = Now;
            GenerateCustodyOffers(map);
            GenerateCollectionOffers(map);
        }

        private void GenerateCustodyOffers(Map map)
        {
            custodyOffers.Clear();
            float wealth = BankUtility.ColonyWealth(map);
            int unit = BankUtility.BullionUnitValue;
            for (int i = 0; i < TermDays.Length; i++)
            {
                int days = TermDays[i];
                float holding = wealth * S.custodyHoldingWealthFraction * (1f + 0.25f * i) * Rand.Range(0.85f, 1.15f);
                int bars = Mathf.Clamp(Mathf.RoundToInt(holding / unit), 1, BoR_DefOf.BoR_Bullion.stackLimit);
                custodyOffers.Add(new CustodyContract
                {
                    durationDays = days,
                    bullionCount = bars,
                });
            }
        }

        private void GenerateCollectionOffers(Map map)
        {
            collectionOffers.Clear();
            Map home = map ?? BankUtility.RichestHomeMap();
            if (home == null) return;
            var candidates = Find.WorldObjects.Settlements
                .Where(s => s.Faction != null && s.Faction != Faction.OfPlayer && !s.Faction.Hidden && !s.Faction.defeated)
                .OrderBy(s => Find.WorldGrid.ApproxDistanceInTiles(s.Tile, home.Tile))
                .Take(12)
                .InRandomOrder()
                .Take(3);
            foreach (Settlement s in candidates)
            {
                collectionOffers.Add(new CollectionContract
                {
                    target = s,
                    debt = Mathf.RoundToInt(Rand.Range(1500f, 6000f) / 50f) * 50,
                    durationDays = 30,
                });
            }
        }

        // ---------------------------------------------------------------- custody

        /// <summary>Starts placement mode so the player picks where the bank crate goes.</summary>
        public void BeginCustodyPlacement(Map map, CustodyContract offer, bool payInCredit)
        {
            if (custody != null || !CanUseServices || map == null || !custodyOffers.Contains(offer)) return;
            offer.payInCredit = payInCredit;
            Current.Game.CurrentMap = map;
            Find.DesignatorManager.Select(new Designator_PlaceBankPallet(map, offer));
            Messages.Message("Choose where the bank should set down its crate.", MessageTypeDefOf.NeutralEvent, false);
        }

        public bool TryAcceptCustody(Map map, CustodyContract offer, IntVec3 palletCell)
        {
            if (custody != null || !CanUseServices || map == null || !custodyOffers.Contains(offer)) return false;
            custodyOffers.Remove(offer);
            custody = offer;
            custody.startTick = Now;
            custody.endTick = Now + offer.durationDays * GenDate.TicksPerDay;
            custody.mapId = map.uniqueID;
            custody.palletCell = palletCell;
            custody.collectionDispatched = false;
            custody.heistsLaunched = 0;
            custody.heistTicks.Clear();

            // Spread heists evenly across the contract, never in the first two days or the last day.
            int heists = Mathf.Max(1, offer.durationDays / GenDate.DaysPerQuadrum);
            int windowStart = custody.startTick + 2 * GenDate.TicksPerDay;
            int windowLen = custody.endTick - GenDate.TicksPerDay - windowStart;
            for (int i = 0; i < heists; i++)
            {
                int segStart = windowStart + windowLen * i / heists;
                int segEnd = windowStart + windowLen * (i + 1) / heists;
                custody.heistTicks.Add(Rand.Range(segStart, segEnd));
            }

            BankShuttleUtility.SendShuttle(map, palletCell, ShuttleMission.Deliver, offer.bullionCount);
            Find.LetterStack.ReceiveLetter("Vault custody contract",
                $"A {BankUtility.BankName} shuttle is landing to set down a crate of {offer.bullionCount} silver bars (worth {BankUtility.Money(offer.HoldingValue)}).\n\n" +
                $"Keep it safe for {offer.durationDays} days and you will be paid {offer.RewardLabel}. The shuttle will return to collect it; every bar missing then will be charged at {S.theftPenaltyMultiplier:0.##}x its value.\n\n" +
                "Word of a bank vault travels fast. Expect far more raids than usual, and at least one organised bank heist.",
                LetterDefOf.NeutralEvent, new LookTargets(palletCell, map));
            return true;
        }

        private void TickCustody(int now)
        {
            if (custody == null) return;
            Map map = custody.Map;

            if (now >= custody.endTick)
            {
                if (!custody.collectionDispatched)
                {
                    custody.collectionDispatched = true;
                    if (BankShuttleUtility.SendShuttle(map, custody.palletCell, ShuttleMission.Collect, 0))
                    {
                        Messages.Message($"The {BankUtility.BankName} shuttle is coming to collect its silver.", new LookTargets(custody.palletCell, map), MessageTypeDefOf.NeutralEvent);
                        return;
                    }
                }
                // No map to land on, or the shuttle never made it: settle on what the colony holds.
                if (map == null || now >= custody.endTick + GenDate.TicksPerDay) SettleCustody();
                return;
            }
            if (map == null) return;

            for (int i = custody.heistTicks.Count - 1; i >= 0; i--)
            {
                if (now < custody.heistTicks[i]) continue;
                if (TryLaunchHeist(map))
                {
                    custody.heistTicks.RemoveAt(i);
                    custody.heistsLaunched++;
                }
                else
                {
                    custody.heistTicks[i] = now + GenDate.TicksPerHour * 6;
                }
            }

            if (Rand.MTBEventOccurs(S.custodyExtraRaidMtbDays, GenDate.TicksPerDay, TickInterval))
            {
                BankUtility.FireRaid(map, 1f, "Raid: vault rumours",
                    $"Rumours of the {BankUtility.BankName} holding stored in your colony have drawn raiders hoping for an easy score.");
            }
        }

        public bool TryLaunchHeist(Map map)
        {
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
            parms.forced = true;
            return BoR_DefOf.BoR_BankHeistRaid.Worker.TryExecute(parms);
        }

        public void SettleCustody()
        {
            CustodyContract c = custody;
            if (c == null) return;
            custody = null;
            int present = BankUtility.CountPlayerBullion(destroy: true);
            int missing = Mathf.Max(0, c.bullionCount - present);
            float penalty = missing * BankUtility.BullionUnitValue * S.theftPenaltyMultiplier;

            string text = $"The {BankUtility.BankName} shuttle has loaded up the bank's crate and left.\n\n" +
                          $"Bars entrusted: {c.bullionCount}\nBars returned: {Mathf.Min(present, c.bullionCount)}\n" +
                          $"Reward: {c.RewardLabel}\n";
            if (missing > 0) text += $"Penalty for {missing} missing bars: -{BankUtility.Money(penalty)}\n";

            // The penalty eats into the reward first, in whatever form it was paid.
            float reward = c.Reward;
            float covered = Mathf.Min(reward, penalty);
            reward -= covered;
            float shortfall = penalty - covered;

            if (reward > 0f)
            {
                if (c.payInCredit)
                {
                    storeCredit += reward;
                    text += $"\n{BankUtility.Money(reward)} of store credit is waiting for you at the Rimica Commodity Exchange.";
                }
                else
                {
                    balance += reward;
                    text += $"\n{BankUtility.Money(reward)} has been credited to your savings account.";
                }
            }

            if (shortfall < 0.5f)
            {
                if (missing == 0) creditRating = Mathf.Min(2f, creditRating + 0.15f);
                Find.LetterStack.ReceiveLetter("Custody contract complete", text, missing == 0 ? LetterDefOf.PositiveEvent : LetterDefOf.NeutralEvent);
                return;
            }

            float seized = Mathf.Min(balance, shortfall);
            balance -= seized;
            shortfall -= seized;
            creditRating = Mathf.Max(0.25f, creditRating - 0.3f);
            if (seized > 0f) text += $"\n{BankUtility.Money(seized)} has been taken from your savings.";
            if (shortfall >= 0.5f)
            {
                AddDebt(shortfall, "Custody shortfall");
                text += $"\nThe remaining {BankUtility.Money(shortfall)} has been added to your debt" +
                        (bountyActive ? " under the bounty contract." : ". Repay it before the due date or face a bounty contract.");
            }
            Find.LetterStack.ReceiveLetter("Custody contract: losses", text, LetterDefOf.NegativeEvent);
        }

        // ---------------------------------------------------------------- commodity shop

        /// <summary>Spends store credit on an order, delivered by drop pod. Leftover credit stays as credit.</summary>
        public bool TryPlaceOrder(Map map, Dictionary<ShopEntry, int> cart)
        {
            if (map == null || cart.Count == 0) return false;
            float total = cart.Sum(kv => kv.Key.Price * kv.Value);
            if (total > storeCredit + 0.01f) return false;
            storeCredit -= total;
            var things = new List<Thing>();
            foreach (KeyValuePair<ShopEntry, int> kv in cart)
            {
                if (kv.Value > 0) things.AddRange(CommodityShop.MakeThings(kv.Key, kv.Value));
            }
            IntVec3 spot = DropCellFinder.TradeDropSpot(map);
            DropPodUtility.DropThingsNear(spot, map, things);
            Find.LetterStack.ReceiveLetter("Order dispatched",
                $"The Rimica Commodity Exchange has dropped your order ({things.Count} pods' worth, {BankUtility.Money(total)} of credit). Remaining store credit: {BankUtility.Money(storeCredit)}.",
                LetterDefOf.PositiveEvent, new LookTargets(spot, map));
            return true;
        }

        // ---------------------------------------------------------------- debt collection

        public bool TryAcceptCollection(CollectionContract offer)
        {
            if (collection != null || !CanUseServices || !collectionOffers.Contains(offer) || offer.TargetGone) return false;
            collectionOffers.Remove(offer);
            collection = offer;
            collection.deadlineTick = Now + offer.durationDays * GenDate.TicksPerDay;
            Find.LetterStack.ReceiveLetter("Debt collection contract",
                $"The {BankUtility.BankName} has hired you to collect a defaulted debt of {BankUtility.Money(offer.debt)} from {offer.target.LabelCap} ({offer.target.Faction.Name}).\n\n" +
                $"Send a caravan to the settlement and use 'Collect debt' — your best negotiator will press them to pay, and you keep a {S.collectionCommission.ToStringPercent()} commission ({BankUtility.Money(offer.Commission)}).\n\n" +
                $"If they refuse, the bank authorises you to seize their assets: destroy the settlement and the bank will pay a bonus commission.\n\nDeadline: {offer.durationDays} days.",
                LetterDefOf.NeutralEvent, offer.target);
            return true;
        }

        public void CancelCollection(bool silent = false)
        {
            if (collection == null) return;
            collection = null;
            if (!silent) creditRating = Mathf.Max(0.25f, creditRating - 0.05f);
        }

        private void TickCollection(int now)
        {
            if (collection == null) return;
            if (collection.TargetGone)
            {
                int reward = Mathf.RoundToInt(collection.Commission * 1.5f);
                balance += reward;
                creditRating = Mathf.Min(2f, creditRating + 0.1f);
                Find.LetterStack.ReceiveLetter("Debtor assets seized",
                    $"The defaulted settlement is no more. The {BankUtility.BankName} has recovered what it could and credited your savings with a {BankUtility.Money(reward)} commission.",
                    LetterDefOf.PositiveEvent);
                collection = null;
                return;
            }
            if (now >= collection.deadlineTick)
            {
                Find.LetterStack.ReceiveLetter("Debt collection failed",
                    $"You did not collect the debt from {collection.target.LabelCap} in time. The {BankUtility.BankName} has reassigned the contract and noted the failure on your credit record.",
                    LetterDefOf.NegativeEvent);
                collection = null;
                creditRating = Mathf.Max(0.25f, creditRating - 0.1f);
            }
        }

        /// <summary>A caravan at the debtor settlement leans on them to pay.</summary>
        public void AttemptCollection(Caravan caravan)
        {
            CollectionContract c = collection;
            if (c == null || c.attempted || c.TargetGone) return;
            Settlement s = c.target;
            Pawn negotiator = BestCaravanPawnUtility.FindBestNegotiator(caravan);
            float chance = CollectionChance(caravan, negotiator);
            c.attempted = true;

            if (Rand.Chance(chance))
            {
                Thing silver = ThingMaker.MakeThing(ThingDefOf.Silver);
                silver.stackCount = c.Commission;
                CaravanInventoryUtility.GiveThing(caravan, silver);
                s.Faction.TryAffectGoodwillWith(Faction.OfPlayer, -10);
                creditRating = Mathf.Min(2f, creditRating + 0.1f);
                collection = null;
                Find.LetterStack.ReceiveLetter("Debt collected",
                    $"{(negotiator != null ? negotiator.LabelShort : "Your caravan")} convinced {s.LabelCap} to pay its debt of {BankUtility.Money(c.debt)} to the {BankUtility.BankName}.\n\n" +
                    $"Your caravan keeps a commission of {BankUtility.Money(c.Commission)}. {s.Faction.Name} is not pleased.",
                    LetterDefOf.PositiveEvent, caravan);
            }
            else
            {
                s.Faction.TryAffectGoodwillWith(Faction.OfPlayer, -20);
                Find.LetterStack.ReceiveLetter("Debtor refuses",
                    $"{s.LabelCap} refused to pay and threw your collectors out. {s.Faction.Name} is furious.\n\n" +
                    $"The {BankUtility.BankName}'s contract still stands: destroy the settlement before the deadline to seize its assets.",
                    LetterDefOf.NegativeEvent, caravan);
            }
        }

        public static float CollectionChance(Caravan caravan, Pawn negotiator)
        {
            float negotiation = negotiator != null ? negotiator.GetStatValue(StatDefOf.NegotiationAbility) : 0f;
            int fighters = caravan.PawnsListForReading.Count(p => p.IsColonist && !p.Downed && !p.WorkTagIsDisabled(WorkTags.Violent));
            return Mathf.Clamp(0.15f + 0.45f * negotiation + 0.03f * fighters, 0.05f, 0.95f);
        }
    }
}
