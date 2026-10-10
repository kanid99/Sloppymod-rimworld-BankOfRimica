# Bank of Rimica — working notes

RimWorld 1.6 mod (requires Odyssey + Vanilla Quests Expanded - Deadlife; VFE Pirates optional).
C# source in `Source/BankOfRimica`, XML in `Common/`, compiled DLL committed to `1.6/Assemblies/`.

## Build
- `Source/BankOfRimica/build.sh` (needs the .NET SDK; game references come from the `Krafs.Rimworld.Ref` NuGet package).
- Always rebuild and commit `1.6/Assemblies/BankOfRimica.dll` with source changes so the repo works as a drop-in mod (RimSort installs it straight from GitHub).

## Release workflow (every update pass)
1. Create a snapshot branch for the update: `snapshot/YYYY-MM-DD-<short-description>`, pointing at the update's final commit, and push it.
2. Push the update to `main` (fast-forward; never force-push `main`).

`main` is what players get, so it must always build and load.
