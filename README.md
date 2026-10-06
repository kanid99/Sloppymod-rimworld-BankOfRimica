# Bank of Rimica — a space bank for RimWorld

A RimWorld mod (1.5 and 1.6) that adds an orbital bank your colony can save with, borrow from, work for, and get hunted by.

No Harmony dependency. Safe to add to an existing save.

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
- Hold the bank's reserves for **15, 30 or 60 days** (a quadrum, half a year or a year) for a fee (default 10% of the holding per quadrum).
- The holding drops in as physical **Rimica bank bullion**. You can't sell it, but it counts toward colony wealth, so raids get bigger.
- While you hold it, extra raids come (default MTB of 6 days), plus at least one **Bank Heist** per quadrum.
- **Bank Heist** is a new raid type at double strength (configurable). The raiders defend themselves at close range, but their goal is the bullion. They bash through doors, grab as many bars as they can carry and sprint for the map edge. If they take heavy losses or a day passes, they call it off and flee.
- When the contract ends, the bank collects its bullion. Each missing bar (stolen, lost or carried off) costs you **1.25× its value**. The penalty comes out of your fee first, then your savings. Anything left over becomes debt, and if you don't pay that debt you get a bounty contract.

All the numbers above can be changed under **Options → Mod settings → Bank of Rimica**.

## Layout

```
About/About.xml
LoadFolders.xml
Common/Defs, Common/Patches     XML shared by all versions
1.5/Assemblies, 1.6/Assemblies  compiled BankOfRimica.dll per game version
Source/BankOfRimica             C# source
```

## Building

You need the .NET SDK (6+). The game's reference assemblies come from the `Krafs.Rimworld.Ref` NuGet package, so you don't need a copy of the game to compile.

```
Source/BankOfRimica/build.sh        # builds 1.5 and 1.6 DLLs into their Assemblies folders
```

## Notes / known limitations
- Art is borrowed from vanilla for now: the uplink uses the comms console texture and the bullion uses a tinted gold texture.
- Text is English only and not yet in translation keys.
