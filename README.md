# OpenWorkshoppa

Community-maintained recovery of Workshoppa, a Dalamud plugin for assisting with Company Workshop turn-ins.

This repository was reconstructed from a released `9.2.0.0` plugin binary and ported to Dalamud API 15. Original authorship and source licensing could not be verified from the recovered distribution, so no new licence is asserted here.

## Development

Build the solution with the .NET 10 SDK. The plugin project targets Dalamud API 15 and includes the recovered `LLib` support library as a project reference.

```powershell
dotnet build .\OpenWorkshoppa.sln -c Release
```

The release build produces an installable plugin archive in the Workshoppa output directory.

## Runtime verification

The automated build validates compilation and packaging only. Before release, test the material delivery, repair kit, and ceruleum tank flows in-game: those paths call game UI callbacks whose payload layouts can change with game patches.
