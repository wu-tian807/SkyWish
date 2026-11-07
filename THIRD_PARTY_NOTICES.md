# Third-party code used by CityDirector

## nachocheesedog/cs2-mcp

- Upstream: https://github.com/nachocheesedog/cs2-mcp
- Pinned commit: `20bb131ba863e3e349b59d6dbdaec6a98aebb27f`
- License: MIT, Copyright (c) 2026 nachocheesedog.
- Full license: [LICENSE.nachocheesedog](src/asset-pipeline/vendor/LICENSE.nachocheesedog).
- `src/asset-pipeline/vendor/cs2_validate.py`: copied validation portion of upstream `server/assets.py` (lines 1–214). No game-control or publishing code is included.
- `src/CityDirector.Mod/AssetImport.cs`: native import call, prefab factory inspection and prefab registry lookup adapted from upstream `mod/AssetImport.cs`. The managed catalog, duplicate refusal, durable job receipts, restricted package scope and backup logic are our additions.
- `scripts/Build-DemoAsset.py`: FBX export settings follow the upstream tested export recipe. Bench geometry, preview and task output are new.
- `src/CityDirector.Mod/AssetMenus.cs`: live MemorySource localization follows upstream `mod/AssetImport.cs`. Early ImageAsset icon creation also followed that implementation; the current local UI icon hosting replaces it. Native menu enumeration, restricted category reassignment, explicit toolbar refresh and per-asset replay configuration are our additions, checked against the locally installed game API.

Other downloaded repositories remain references; they are not dependencies of this milestone. Unity and game assemblies are referenced from the local installation, not redistributed.
