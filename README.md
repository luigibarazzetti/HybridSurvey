# HybridSurvey — Single View + Control Panel

Rhino 8 / Windows plugin for Gaussian Splatting PLY and Rhino point clouds.

## Commands
- `HybridPanel` — open modeless graphical control panel (Rhino remains interactive).
- `HybridSplatImport` — import Gaussian PLY.
- `HybridSplatClear` — remove the currently loaded Gaussian from renderer memory (does not delete PLY on disk).
- `HybridSplatShow` — toggle Gaussian visibility.
- `HybridSplatOpacity` — set Gaussian opacity 0–100.
- `HybridCloudImport` — import cloud using available Rhino importers.
- `HybridCloudShow` — toggle point-cloud objects imported through the plugin.

No Snap, Dual View, linked view or clipping features.

## GitHub
Upload the **contents** of this directory to the repository root, including `.github/workflows/build.yml`. In Actions run **Build HybridSurvey Rhino 8**, then download the artifact. Keep the `.rhp` and its dependency DLLs together. Load the `.rhp` through Rhino's PluginManager, then type `HybridPanel`.

## Known limitations
- Not yet build-tested on Windows/Rhino 8; API compatibility must be validated by GitHub Actions.
- Cloud visibility tracking is session-local and based on imported object IDs.
- Opacity changes rebuild the Gaussian render buffer and may take time on large files; use **Apply Opacity** instead of continuous slider updates.
- Third-party renderer dependencies require a redistribution/license review before public release.
