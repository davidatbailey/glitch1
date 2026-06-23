# glitch1 Implementation Checklist

## Milestone 1: Working CodeLab Exemplar

- [x] Create CodeLab source file for `glitch1`.
- [x] Target CodeLab `ClassicEffect` / CPU `Surface` render API.
- [x] Add core UI controls:
  - [x] `Seed number` slider from `1` to `10000`.
  - [x] `Allow JPEG Block Panic` checkbox.
  - [x] `Allow Missing Packets` checkbox.
  - [x] `Allow Channel Swap` checkbox.
  - [x] `Allow Lack Of Clamping` checkbox.
  - [x] `Allow Memory Alignment Tear` checkbox.
  - [x] `Allow Single Channel Transform` checkbox.
  - [x] `Allow JPEG Binary Damage` checkbox.
  - [x] `Stack effects` checkbox.
- [x] Use the seed to choose active effects from the allowed checkbox pool.
- [x] Fall back to the first allowed effect if a seed selects no effects.
- [x] Use CodeLab-style `Amount1`, `Amount2`, etc. UICode bindings so UI changes propagate into render code.
- [x] Enable only `JPEG Block Panic` by default.
- [x] Derive hidden controls from the seed:
  - [x] Intensity.
  - [x] Block size.
  - [x] Mask amount.
  - [x] Mask scale.
  - [x] Mask phase.
- [x] Add deterministic helper functions:
  - [x] Coordinate hash.
  - [x] Block hash.
  - [x] `Random01` style helper.
  - [x] Clamp and wrap helpers.
  - [x] Source sampling helper.
- [x] Add Nebulous-inspired mask:
  - [x] Layered sine wave field.
  - [x] Seeded block turbulence.
  - [x] Soft mask output from `0.0` to `1.0`.
  - [x] Seed-derived mask amount keeps the effect partially global.
- [x] Add initial checkbox effects:
  - [x] JPEG Block Panic using a real JPEG encode/decode round trip.
- [x] JPEG Binary Damage using deterministic in-memory byte corruption.
  - [x] Missing Packet Rows.
  - [x] Channel Swap Lab.
  - [x] Lack of Clamping.
  - [x] Memory Alignment Tear.
  - [x] Single Channel Transform.
- [x] Add composition mode:
  - [x] Stack mode uses a seed-determined effect order.
  - [x] Non-stack mode uses fixed UI order.
  - [x] Spatial sampling effects still sample from `src` to preserve deterministic rendering.
- [x] Expand `Channel Swap` to include:
  - [x] RGB channel swaps.
  - [x] Fake HSK component swaps.
  - [x] RGB/HSK cross-wiring variants.
  - [x] One seed-selected variant applied across the whole image.
- [x] Preserve alpha in every first-version effect.
- [x] Blend original and corrupted RGB using seed-derived intensity and mask.
- [x] Avoid render-order-dependent random state.
- [x] Avoid per-pixel object allocation.
- [x] Build the real JPEG round-trip surface outside the inner pixel loop.
- [x] JPEG Binary Damage encodes and corrupts the accumulated stack surface.
- [x] Dispatch only enabled effects inside the per-pixel loop.
- [x] Skip mask calculation when only full-surface JPEG modes are enabled.
- [x] Skip the normal per-pixel output path after JPEG Binary Damage produces the final damaged stack.
- [x] Remove persistent JPEG cache while debugging CodeLab UI refresh behavior.
- [x] Apply JPEG artifacts at full strength when the JPEG checkbox is enabled.
- [x] Replace red UI-binding diagnostic with real JPEG path after checkbox/seed updates are confirmed.
- [ ] Reintroduce safe caching only after UI changes are confirmed to refresh correctly.
- [ ] Optimize real JPEG construction with `LockBits` if `SetPixel` / `GetPixel` is too slow.
- [x] Use a multi-pass renderer for JPEG Binary Damage so it applies to the accumulated stack result.
- [ ] Resolve random seed default behavior in CodeLab UI.

## Milestone 2: Expanded Glitch Effects

- [ ] Duplicated Packet Echo.
- [ ] RGB to HSK Misread.
- [ ] Floating Point Drift.
- [ ] Power Interruption.
- [ ] Rotate Then De-Rotate Damage.
- [ ] Off-By-One Scanline.
- [ ] Endianness Bloom.

## Milestone 3: Polish And Documentation

- [ ] Add concise comments explaining deterministic hashing.
- [ ] Add comments explaining the mask.
- [ ] Document CodeLab import/use steps.
- [ ] Document all mode numbers and names.
- [ ] Add manual testing notes.
- [ ] Add performance notes based on implementation details.

## Validation Checklist

- [ ] Same seed and settings produce identical output.
- [ ] Different seeds produce visibly different output.
- [ ] `Intensity = 0` returns the original image.
- [ ] `Mask Amount = 0` applies the selected effect globally.
- [ ] Small images do not crash.
- [ ] Transparent pixels preserve alpha.
- [ ] Boundary pixels do not sample outside the source surface.
- [ ] Every Milestone 1 mode has a visible effect at high intensity.
