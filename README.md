# Bank of Rimica — a space bank for RimWorld

A RimWorld 1.6 mod that adds an orbital bank your colony can save with, borrow from, work for, and get hunted by.

**Requires the Odyssey DLC and [Vanilla Quests Expanded - Deadlife](https://github.com/Vanilla-Expanded/VanillaQuestsExpanded-Deadlife)** (which itself needs Anomaly, Vanilla Expanded Framework and Harmony). No Harmony dependency. Safe to add to an existing save.
Optional: [Vanilla Factions Expanded - Pirates](https://steamcommunity.com/sharedfiles/filedetails/?id=2723801948). If it's installed, bank heists can arrive on gauntlet ships.

## Features

Build a **Rimica bank uplink** (Misc tab, requires *Microelectronics*, 200 W power) and have a colonist use it to open the bank window.

### Bank opinion
- The bank keeps an **opinion** of your colony, from -100 to +100, starting at 0. It's shown in the bank window with a tier: blacklisted, distrustful, wary, neutral, valued client, trusted client or preferred partner.
- **Raised by:**
  - loans repaid on time (+6)
  - debts collected (+8)
  - debtor assets seized (+6)
  - custody contracts with every bar returned (+5 per quadrum)
  - paying off a bounty (+10)
- **Lowered by:**
  - defaulting (-40)
  - custody losses (-5, or -15 if they exceed the reward)
  - missed or abandoned collections (-6 / -3)
  - late repayment (-3)
- **Effect on rates:** at +100, loan interest is halved and deposit interest doubled. At -100, loans cost 1.5× and deposits earn nothing. Both strengths are adjustable in settings.
- The separate **credit rating** still sets how much you can borrow.

### Savings account
- Deposit silver from within range of a powered **orbital trade beacon**; withdrawals arrive by drop pod.
- Earns interest daily (default 3% per quadrum, compounding).

### Loans
- Credit limit scales with colony wealth and your **credit rating** (0.25–2.0).
- Terms of 15, 30 or 60 days (default 10% interest per quadrum). The silver arrives by drop pod.
- Repay from beacon silver or from savings, in full or in part. On-time repayment raises your credit rating.
- Missing the due date adds a late fee and starts a grace period (default 3 days).

### Default and bounty contracts
If you miss the grace period, or press **Refuse to pay**:
- The bank seizes your savings to cover the debt.
- Whatever is left, plus a 25% default penalty, becomes an **open bounty contract** on your colony.
- Bounty hunter raids keep coming (default every ~7 days), and each band is bigger than the last.
- It ends only when you pay the debt in full through an uplink (or your colony is wiped out). Until then every other banking service is suspended.

### Debt collection contracts
- The bank offers contracts against nearby settlements that have defaulted.
- Send a caravan there and use the **Collect debt** button. Your best negotiator, backed by your fighters, tries to make them pay. If they pay, the caravan keeps a 25% commission. Either way, that faction's goodwill drops.
- If they refuse, **destroy the settlement** before the deadline. The bank then pays a bonus commission into your savings.

### Vault custody contracts (and Bank Heists)
- Host the bank's holdings for **15, 30 or 60 days** (a quadrum, half a year or a year). When you accept, choose your reward:
  - **1% of the holding in silver per quadrum held**, paid into your savings, or
  - **5% of the holding per quadrum as store credit** at the **Rimica Commodity Exchange** (see below).
  - Reward and risk both grow with length. A 60-day contract pays four times the rate of a 15-day one, but it holds more silver, brings four heists instead of one, and each heist hits 25% harder than the last.
- When you accept, you **choose where the bank's 2x2 crate goes**. A Bank of Rimica shuttle lands nearby, sets down the crate and loads it with **Rimica silver bars**.
  - The crate uses Deadlife's military crate art: filled with silver while it holds bars, empty once they're gone. Bars stored in it are drawn as part of the crate, with the bar count still shown.
  - Each bar is worth 10,000 silver, and up to 100 fit in one stack.
  - Bars outside the crate (moved, dropped or carried off) use vanilla gold's bar art recoloured to silver, so a pile looks bigger or smaller depending on how many bars are in it.
- The bars can't be sold. They count toward colony wealth, so raids get bigger.
- While you hold them, extra raids come (default MTB of 6 days), plus at least one **Bank Heist** per quadrum.
- **Bank Heist** is a new raid type at double strength (configurable).
  - The raiders defend themselves at close range, but their goal is the silver. They bash through doors and grab what they can carry.
  - **Getaways:** the first time a raider picks up silver, a **getaway shuttle** lands somewhere outside your home area. In a gauntlet heist, the robbers' crashed **junker ships** are the way out instead.
  - Raiders carrying silver split up: most run for the nearest getaway craft and board it, and the rest run for the map edge on foot. A raider who can't reach a craft runs for the edge too.
  - **Warcasket cover force:** raiders in VFE Pirates warcaskets (the junkers' heavy troops) don't steal. They attack the colony to keep you busy while the foot raiders go for the silver. Once every thief has boarded, escaped or died, the warcaskets fall back to their ships, board, and leave too. The craft waits for them.
  - The craft lifts off once everyone left on the ground has boarded. Without a cover force still fighting, it also lifts off an hour after the first raider boards. Once it's gone, so are the raiders and the silver aboard. Gauntlet ships take off again using their own flight art.
  - **Destroy the craft before it takes off** and everyone aboard spills out of the wreck, still carrying the silver.
  - If they take heavy losses or a day passes, they call off the heist and flee.
  - With VFE Pirates installed, a heist may crash in on **gauntlet ships**, flown by the junkers if they're hostile (otherwise pirates or mercenaries). The chance is 15% normally, and 75% once you hold **1,000,000+ silver** for the bank. The raiders switch back to the heist AI once they're out of the wrecks. You can change both chances, or turn gauntlet heists off, in the mod settings.
- When the contract ends, the shuttle comes back, loads the bars and the crate, and leaves.
  - Each missing bar (stolen, lost or carried off) costs you **1.25× its value**.
  - The penalty comes out of your reward first (silver or credit), then your savings. Anything left over becomes debt, and if you don't pay that debt you get a bounty contract.

### Rimica Commodity Exchange
- A shop tab in the bank window that sells nearly every item in the game, including mod items. Furniture and other movable buildings come packed up, ready to place.
  - Anything made from a material uses that item's default material.
  - **Quality:** items with quality have a quality picker in their row. Better quality costs steeply more: Awful 0.5×, Poor 0.75×, Normal 1×, Good 1.5×, Excellent 2.5×, Masterwork 5× and Legendary 10× the normal price, before the shop's 2× markup. The cart keeps each quality as its own line.
  - Silver isn't sold, so credit can't be turned back into silver.
- Buy with store credit earned from custody contracts. Search by name, filter by category, fill a cart (shift-click for 10, ctrl-click for 100), and the order arrives by drop pod.
- Everything costs **2× market value**. Unspent credit stays on your account and is never paid out as silver.

**Optional:** the custody reward rates and the shop price multiplier keep their defaults (1% / 5% per quadrum, 2× prices) unless you tick **Customise rewards and shop prices** at the top of the mod settings. Ticking it shows sliders and a reset button.

All the other numbers above can be changed under **Options → Mod settings → Bank of Rimica**.

## Roadmap
- **Join the bank / open a branch:** a long-term goal. A colony with high opinion could join the Bank of Rimica and run its own branch.

## Layout

```
About/About.xml
LoadFolders.xml
Common/Defs, Common/Patches     XML shared by all versions
1.6/Assemblies                  compiled BankOfRimica.dll
Source/Art                      script that generates the mod's own textures
Source/BankOfRimica             C# source
```

## Building

You need the .NET SDK (6+). The game's reference assemblies come from the `Krafs.Rimworld.Ref` NuGet package, so you don't need a copy of the game to compile.

```
Source/BankOfRimica/build.sh        # builds 1.6/Assemblies/BankOfRimica.dll
```

## Art
- **Silver bars:** vanilla gold bars, desaturated to silver when the game loads. Vanilla gold isn't affected.
- **Bank crate:** Vanilla Quests Expanded - Deadlife's `Loot_LargeMilitaryCrate_Silver` and `Loot_LargeMilitaryCrate_Empty` textures.
- **Bank shuttle:** Odyssey's passenger shuttle (`PassengerShuttle`), if found at startup. Otherwise the mod's own shuttle texture.
- The mod's own textures come from `Source/Art/make_bank_art.py`. The log says which art was used.

## Notes / known limitations
- The uplink uses the vanilla comms console texture.
- Text is English only and not yet in translation keys.
