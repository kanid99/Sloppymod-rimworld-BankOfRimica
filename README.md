# Bank of Rimica — a space bank for RimWorld

A RimWorld 1.6 mod that adds an orbital bank your colony can save with, borrow from, work for, and get hunted by.

**Requires the Odyssey DLC and [Vanilla Quests Expanded - Deadlife](https://github.com/Vanilla-Expanded/VanillaQuestsExpanded-Deadlife)** (which itself needs Anomaly, Vanilla Expanded Framework and Harmony). No Harmony dependency. Safe to add to an existing save.
Optional: [Vanilla Factions Expanded - Pirates](https://steamcommunity.com/sharedfiles/filedetails/?id=2723801948). If it's installed, bank heists arrive on gauntlet ships.

## Features

Build a **Rimica bank uplink** (Misc tab, requires *Microelectronics*, 200 W power) and have a colonist use it to open the bank window.

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
- Host the bank's holdings for **15, 30 or 60 days** (a quadrum, half a year or a year) for a fee (default 10% of the holding per quadrum).
- When you accept, you **choose where the bank's 2x2 crate goes**. A Bank of Rimica shuttle lands nearby, sets down the crate and loads it with **Rimica silver bars**.
  - The crate uses Deadlife's military crate art: filled with silver while it holds bars, empty once they're gone. Bars stored in it are drawn as part of the crate, with the bar count still shown.
  - Each bar is worth 10,000 silver, and up to 100 fit in one stack.
  - Bars outside the crate (moved, dropped or carried off) use vanilla gold's bar art recoloured to silver, so a pile looks bigger or smaller depending on how many bars are in it.
- The bars can't be sold. They count toward colony wealth, so raids get bigger.
- While you hold them, extra raids come (default MTB of 6 days), plus at least one **Bank Heist** per quadrum.
- **Bank Heist** is a new raid type at double strength (configurable).
  - The raiders defend themselves at close range, but their goal is the silver. They bash through doors, grab what they can carry and sprint for the map edge.
  - If they take heavy losses or a day passes, they call it off and flee.
  - With VFE Pirates installed, they crash in on **gauntlet ships**, then switch back to the heist AI once they're out of the wrecks. You can turn this off in the mod settings.
- When the contract ends, the shuttle comes back, loads the bars and the crate, and leaves.
  - Each missing bar (stolen, lost or carried off) costs you **1.25× its value**.
  - The penalty comes out of your fee first, then your savings. Anything left over becomes debt, and if you don't pay that debt you get a bounty contract.

All the numbers above can be changed under **Options → Mod settings → Bank of Rimica**.

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
