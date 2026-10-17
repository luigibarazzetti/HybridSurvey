# HybridSurvey — Gaussian + Point Cloud (single-view edition)

Rhino 8 / Windows plugin. Based on the earlier Gaussian Only + Opacity source.

## Commands

- `HybridSplatImport`: import a Gaussian Splatting `.ply` using the bundled renderer.
- `HybridSplatOpacity`: set Gaussian opacity, 0–100%.
- `HybridSplatShow`: toggle Gaussian rendering.
- `HybridCloudImport`: open a point-cloud file through Rhino's native/installed importer.
- `HybridCloudShow`: toggle visibility of point-cloud objects imported through `HybridCloudImport`.

All commands operate in ordinary Rhino viewports. No linked cameras, no dual view, no snap tools, no clipping plane, and no separate display panel. You can show the Gaussian splats, the point cloud, or both in the same perspective viewport.

## Notes

- Point-cloud format availability depends on Rhino 8 and installed importers. An extension in the file picker does **not** guarantee that Rhino can import it.
- `HybridCloudShow` controls only point-cloud objects detected as new during `HybridCloudImport`; pre-existing point clouds are not affected.
- The Gaussian PLY loader expects a 3D Gaussian Splatting PLY, not an ordinary point-cloud PLY.
- Source coordinates are not transformed by this plugin. Ensure the GS and cloud have already been registered in the same coordinate system.
- Gaussian rendering is managed by the external `RhinoGaussianSplatting.dll`; it is not stored as native `.3dm` geometry.
- The included third-party binaries require independent license/redistribution verification before publishing the repository publicly.
- **Not tested in a Windows Rhino 8 runtime.** Compile via GitHub Actions and test the import and visibility commands in Rhino.

## GitHub build

Upload all files (including `ThirdParty/` and `.github/workflows/build.yml`) to a GitHub repository. In Actions, run **Build HybridSurvey Rhino 8**. Download the `HybridSurvey-Rhino8` artifact and keep the `.rhp` and accompanying DLLs together.
