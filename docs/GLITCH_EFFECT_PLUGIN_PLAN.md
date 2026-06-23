# glitch1 Paint.NET CodeLab Effect Plugin Plan

**Status**: ✅ Milestone 1 Complete | See [README.md](../README.md) and [IMPLEMENTATION_CHECKLIST.md](IMPLEMENTATION_CHECKLIST.md) for current implementation status.

---

## Goal

Create `glitch1`, a Paint.NET CodeLab effect plugin that applies a family of deterministic glitch effects inspired by real or imagined data bugs. The plugin should be useful as both a creative image effect and a coding exemplar: clear structure, predictable output from a seed, and performance-conscious image processing.

## Core Requirements

- Build as a Paint.NET CodeLab effect plugin.
- Target CodeLab `ClassicEffect`, not `GpuImageEffect`, because the implementation uses the CPU `Surface` render API.
- Ship as one effect with multiple modes, rather than a pack of separate effects.
- Provide several checkbox-enabled glitch effects inspired by data bugs.
- Include a `Seed` control:
  - Slider range: `1` to `1000`.
  - UI label: `Seed number`.
  - Default should be randomized once when the effect dialog opens.
  - Output must be deterministic for a given seed and the same settings.
- Include a mask feature based on a wave/noise pattern similar in spirit to Paint.NET's built-in "Nebulous" texture effect.
- Preserve alpha by default; the first version should corrupt RGB only.
- Keep the code suitable as an exemplar:
  - Easy to read.
  - Clear helper functions.
  - Avoid unnecessary allocations in the render loop.
  - Explain performance tradeoffs in comments where useful.

## Proposed User Controls

Paint.NET CodeLab UI controls are limited compared with a full custom effect UI, so the first version should expose only the seed and effect toggles. Supporting parameters should be derived from the seed so the plugin stays compact and deterministic.

Implementation note:

- Use CodeLab's conventional `Amount1`, `Amount2`, etc. UICode variable names, then map them to readable local names inside `Render`. This avoids stale initial values if CodeLab does not bind custom control variable names reliably.

- `Seed number`: `1` to `10000`.
- `Allow JPEG Block Panic`: checkbox.
- `Allow Missing Packets`: checkbox.
- `Allow Channel Swap`: checkbox.
- `Allow Lack Of Clamping`: checkbox.
- `Allow Memory Alignment Tear`: checkbox.
- `Allow Single Channel Transform`: checkbox.
- `Allow JPEG Binary Damage`: checkbox.
- `Stack effects`: checkbox.

Seed-based selection:

- The effect checkboxes define the allowed effect pool.
- The seed deterministically selects which allowed effects are active.
- If a seed selects no allowed effects, the first allowed effect is enabled as a fallback.
- CodeLab cannot visually tick/untick boxes from `Render`, so the visible checkboxes are allow-list controls rather than live indicators of the seed-selected active set.

Default checkbox state:

- `JPEG Block Panic`: enabled.
- `Stack effects`: enabled.
- All effect allow-list checkboxes: enabled.

Composition behavior:

- When `Stack effects` is enabled, the seed determines the order in which enabled effects are applied.
- When `Stack effects` is disabled, enabled effects are applied in fixed UI order.
- Effects use the current accumulated pixel as their input, so enabled effects build on each other.
- Effects that sample neighboring or transformed pixels should continue sampling from `src`, not `dst`, so rendering stays deterministic and independent of tile order.

Seed-derived hidden parameters:

- Intensity.
- Block size.
- Mask amount.
- Mask scale.
- Mask phase.
- Per-mode variation.

## Deterministic Randomness

The plugin should not use global random state inside the render loop. Instead:

- Derive a local pseudo-random generator from `Seed`.
- Use deterministic integer hashing for per-pixel or per-block randomness.
- Make randomness independent of render order so threaded rendering produces the same result.

Recommended approach:

- Use a small deterministic hash function such as xorshift, Wang hash, or a simple integer avalanche hash.
- Generate random values from coordinates, block coordinates, effect-specific salt values, and seed.
- Example conceptual input:
  - `Hash(seed, x / blockSize, y / blockSize, mode)`

This avoids problems where Paint.NET renders regions in different orders.

## Mask Feature

The mask should act as a spatial selector that decides where the glitch appears and how strong it is.

Inspired by "Nebulous", the mask can combine:

- Sine waves.
- Phase offsets.
- Seeded pseudo-random turbulence.
- Layered frequencies.
- Soft thresholding.

Suggested mask formula:

- Calculate normalized coordinates.
- Combine several wave fields:
  - Horizontal wave.
  - Vertical wave.
  - Diagonal wave.
  - Seeded coordinate noise.
- Convert the result into a mask value from `0.0` to `1.0`.
- Multiply the selected glitch amount by this mask.

Important behavior:

- `Mask Amount = 0` should apply the effect everywhere.
- Higher mask values should make the glitch appear in cloudy, wave-like streaks.
- Mask should be deterministic from seed and mask settings.

## Bug Effects

The first version should prioritize expressive visual variety over strict simulation accuracy. Each mode should be inspired by a data bug, but it does not need to be a faithful low-level reproduction if an approximation gives a stronger, more useful creative result.

### 1. JPEG Block Panic

Uses a real JPEG encode/decode pass at seed-derived high compression.

Behavior:

- Encode the source image to JPEG in memory at a low quality value.
- Decode the JPEG back into a temporary surface.
- Use the decoded JPEG surface at full strength when the checkbox is enabled, so toggling the effect is visually obvious.
- Preserve the original alpha channel because JPEG has no alpha.

Implementation notes:

- This mode should not be approximated with handmade block quantization.
- Build the JPEG round-trip surface outside the inner pixel loop.
- While debugging CodeLab UI refresh behavior, avoid persistent cross-preview JPEG caching.
- A later performance pass may reintroduce caching using image size, seed, quality, and checkbox option state as the cache key.
- Use a very low seed-derived quality range, currently around `2` to `12`, to make high-compression artifacts clear.
- The first implementation may use `Bitmap.SetPixel` / `GetPixel` for readability.
- A later performance pass should consider `LockBits` to reduce conversion cost on large images.
- JPEG quality should be derived from the seed and overall intensity.

### 1b. JPEG Binary Damage

Uses a real JPEG encode pass, then deliberately corrupts the in-memory JPEG byte stream before decoding it.

Behavior:

- Render the enabled non-binary effects into a temporary full-surface stack.
- Encode that accumulated stack to JPEG in memory.
- Locate the entropy-coded image data between the start-of-scan and end-of-image markers.
- Seed-select one binary damage mode:
  - Mutate byte values.
  - Duplicate a short run of bytes.
  - Remove a short run of bytes.
- Decode the damaged JPEG back into a temporary surface.
- If a damaged JPEG becomes undecodable, fall back to the normal JPEG round trip rather than crashing.
- When enabled, output the damaged JPEG made from the accumulated stack.
- If normal JPEG is also checked, it contributes to the accumulated stack before binary damage is applied.

Implementation notes:

- Preserve alpha by applying source alpha after decode.
- Keep corruption deterministic from the seed.
- Avoid changing marker/header bytes when possible so more damaged files remain decodable.
- This effect uses a multi-pass full-surface path, so it is more expensive than the other effects.

### 2. Missing Packet Rows

Inspired by dropped packets in streamed image/video data.

Behavior:

- Random horizontal strips are replaced by neighboring strips, transparent darkening, or previous sampled rows.
- Some strips may shift left or right.
- Strip height should relate to `Block Size`.

Implementation notes:

- Use block-row hashes from `Seed`.
- Avoid reading from pixels already written in this render pass; sample only from `src`.

### 3. Duplicated Packet Echo

Inspired by duplicated network packets.

Behavior:

- Random rectangular blocks are copied from nearby locations.
- Some copied blocks repeat several times in a direction.
- Creates echo trails and repeated visual fragments.

Implementation notes:

- Compute source offsets per block.
- Clamp source coordinates safely unless using the "lack of clamping" mode.

### 4. Channel Swap Lab

Inspired by channel-order bugs and color-space confusion.

Behavior:

- Swap RGB channels, such as:
  - Red and green.
  - Red and blue.
  - Green and blue.
  - Rotate RGB to GBR or BRG.
- Swap fake HSK components:
  - Hue and saturation.
  - Hue and key.
  - Saturation and key.
- Cross-wire RGB and fake HSK components, such as treating saturation or key as an RGB channel.
- Use one seed-selected channel swap variant across the whole image.
- Optionally apply the swap only under the mask.

Implementation notes:

- This mode is cheap and can be combined with other modes later.
- Preserve alpha.
- HSK swaps use the plugin's intentionally fake HSK interpretation, matching the data-bug theme rather than a formal color model.

### 5. RGB to HSK Misread

Inspired by treating RGB values as if they belonged to a strange hue/saturation/key color space.

Behavior:

- Convert RGB-like values into a fake HSK interpretation:
  - `H`: derived from one channel or channel difference.
  - `S`: derived from max/min channel spread.
  - `K`: derived from inverted luminance or a selected channel.
- Convert back through an intentionally wrong formula.

Why HSK:

- HSK is unusual enough to feel like a convincing data-layout bug rather than a normal filter.
- It gives strong, strange color behavior without needing a real standard color space.

Implementation notes:

- Name the mode clearly as a fictional or intentionally misread space.
- Avoid expensive trigonometry per pixel if possible; use simple approximations.

### 6. Floating Point Drift

Inspired by precision loss, rounding drift, and unstable numerical transforms.

Behavior:

- Offset pixels by tiny coordinate errors that accumulate across the image.
- Quantize intermediate values.
- Create stair-stepped waves or subtle tearing.

Implementation notes:

- Use integer or fixed-point math where possible.
- If using floats, keep the calculation simple and deterministic.
- Make the visual error stronger at higher `Intensity`.

### 7. Power Interruption

Inspired by a write operation interrupted mid-frame.

Behavior:

- Part of the image appears correctly rendered.
- After a seeded cutoff point, blocks become stale, darkened, shifted, or partially zeroed.
- Can include hard discontinuities, scanline collapse, or sudden palette corruption.

Implementation notes:

- Choose interruption position from seed.
- Make the cutoff direction selectable later if more controls are available.

### 8. Lack of Clamping

Inspired by overflow, underflow, and missing bounds checks.

Behavior:

- Values exceeding `255` wrap, fold, or bleed into other channels.
- Negative-like values wrap upward.
- Extreme intensity produces posterized neon or inverted bands.

Implementation notes:

- Because Paint.NET color channels are byte-like, simulate the bug intentionally using integer math.
- Provide variants:
  - Wrap: `value & 255`.
  - Fold: reflect values back into range.
  - Saturation bug: clamp only some channels.

### 9. Rotate Then De-Rotate Damage

Inspired by repeated image transforms and interpolation loss.

Behavior:

- Sample the source as if rotating a small amount, then sampling back imperfectly.
- Creates resampling blur, edge jitter, holes, and diagonal artifacts.

Implementation notes:

- Avoid a full two-pass rotation unless necessary.
- Approximate with inverse coordinate sampling and seeded local angle offsets.
- Use nearest-neighbor or cheap bilinear sampling depending on performance budget.

### 10. Off-By-One Scanline

Inspired by row stride mistakes and one-pixel indexing errors.

Behavior:

- Every Nth row or column samples from one pixel off.
- Creates zippering, color crawling, and slight geometric instability.

Implementation notes:

- Very useful as a small, readable exemplar mode.
- Can be mixed with packet loss effects.

### 11. Endianness Bloom

Inspired by reading bytes in the wrong order.

Behavior:

- Treat ARGB bytes as if their order has changed.
- Alpha-like values may affect color brightness, while color values may appear as transparency-like fades.

Implementation notes:

- Preserve actual alpha in the final output unless the user explicitly wants alpha corruption.
- Simulate byte-order confusion on RGB only for safety.

### 12. Memory Alignment Tear

Inspired by reading pixel data from misaligned memory.

Behavior:

- Samples color channels from neighboring pixels:
  - Red from `x - 1`.
  - Green from `x`.
  - Blue from `x + 1`.
- Produces chromatic tearing and fringes.

Implementation notes:

- Clamp or wrap neighbor coordinates depending on intensity.
- This is visually strong and computationally cheap.

### 13. Single Channel Transform

Inspired by applying a geometric operation to only one component of image data.

Behavior:

- Seed selects one component from `R`, `G`, `B`, `H`, `S`, or `K`.
- Seed selects a transform:
  - Horizontal stretch.
  - Center scale.
  - Rotation.
- The selected component is sampled from transformed coordinates and written back into the current pixel.
- RGB channels are replaced directly.
- HSK channels use the plugin's intentionally fake HSK interpretation.

Implementation notes:

- This effect is enabled by one checkbox only; channel and transform choices are seed-derived.
- Preserve alpha.
- Sample from `src` for transformed channel data, then blend into the current result.

## Mode Combination Strategy

The plugin should remain a single effect with checkbox toggles for each available bug-inspired mode. Multiple modes may be enabled at once, and each enabled mode should be blended into the result in a deterministic order.

This keeps the project easy to distribute, keeps shared seed and mask behavior consistent, and works well for a coding exemplar because all modes can reuse the same deterministic hashing, masking, sampling, and blending helpers.

Future version:

- Allow a "Chaos Stack" mode where several bugs are combined.
- Use the seed to choose a deterministic sequence of bug passes.
- Add controls for blend behavior.

## Render Pipeline

Recommended per-pixel flow:

1. Read source pixel from `src`.
2. Calculate mask value for current coordinate.
3. Derive hidden parameters from the seed.
4. For each enabled checkbox mode, calculate mode-specific corrupted color by sampling from `src` or transforming the current RGB result.
5. Blend current and corrupted color using seed-derived intensity and mask.
6. Write final color to `dst`.

Important:

- Always read from `src`, never from `dst`, during render.
- Preserve alpha in the first version.
- Keep alpha corruption out of the default design; any future alpha-corrupting mode should be explicit and clearly named.
- Clamp final channel values before creating the output color.

## Code Structure

Suggested helper functions:

- `Hash(int seed, int x, int y, int salt)`
- `Random01(int seed, int x, int y, int salt)`
- `ClampByte(int value)`
- `WrapByte(int value)`
- `LerpByte(byte a, byte b, double amount)`
- `GetMaskValue(int x, int y, int seed, double scale, double phase)`
- `SampleClamped(Surface src, int x, int y)`
- One function per glitch effect, if CodeLab allows the generated code to stay readable.

If CodeLab makes many helper functions awkward, group related logic into compact static methods.

## Performance Considerations

This plugin should explicitly demonstrate good image-effect performance habits.

- Avoid creating objects inside the inner pixel loop.
- Keep whole-image temporary allocations out of the per-pixel loop. The real JPEG modes may allocate while building their temporary surfaces, but not once per pixel.
- Avoid repeating the normal per-pixel render path after JPEG Binary Damage has already produced the final damaged stack.
- Build the active effect order from enabled effects only, so disabled effects do not add per-pixel dispatch overhead.
- Skip mask calculation when only full-surface JPEG modes are enabled.
- Avoid `new Random()` in rendering code.
- Avoid random sequences that depend on pixel traversal order.
- Prefer integer math for hashes, block lookup, and channel operations.
- Cache repeated settings in local variables before entering loops.
- Compute block-level decisions from block coordinates rather than every pixel where possible.
- Use simple approximations for expensive operations.
- Use trigonometry sparingly in the mask:
  - Consider calculating wave values per row or column where practical.
  - Keep the number of sine calls small.
- Keep source sampling bounded:
  - Clamp source coordinates explicitly.
  - Avoid exception-driven bounds handling.
- Preserve Paint.NET's region-based rendering model:
  - Do not depend on render order.
  - Do not store mutable per-render global state that changes per pixel.
- For modes requiring neighboring pixels, sample from `src` only.
- Prefer a single-pass design for the first version.
- For real JPEG artifacts, precompute a JPEG round-trip surface before entering the pixel loop rather than encoding inside the pixel loop.

## Quality And Testing Plan

Test cases:

- Same seed and settings produce identical output.
- Different seeds produce visibly different output.
- `Intensity = 0` returns the original image.
- `Mask Amount = 0` applies the selected effect globally.
- Small images do not crash.
- Very large images remain responsive.
- Transparent pixels preserve alpha unless alpha corruption is deliberately enabled.
- Boundary pixels do not throw or sample outside the source surface.
- Every enabled effect has a visible impact across a useful range of seed values.

Manual visual checks:

- Portrait/photo.
- Flat-color graphic.
- Text screenshot.
- Transparent PNG.
- Very small image, such as `16x16`.
- Large image, such as `4000x3000`.

## Documentation Notes

The plugin should include comments explaining:

- Why deterministic coordinate hashing is used instead of `Random`.
- How the mask is generated.
- Why some visual effects are approximations of data bugs rather than literal implementations.
- Which operations are intentionally "wrong" for artistic purposes.

## Suggested First Milestone

Implement a focused first version with:

- Seed.
- Effect checkboxes.
- Hidden seed-derived intensity.
- Hidden seed-derived block size.
- Hidden seed-derived mask amount and mask scale.
- Five initial modes:
  - JPEG Block Panic.
  - Missing Packet Rows.
  - Channel Swap Lab.
  - Lack of Clamping.
  - Memory Alignment Tear.

This provides a strong range of visual effects while keeping the first CodeLab implementation manageable.

## Resolved Design Decisions

- Plugin form: one effect with multiple modes.
- Plugin name: `glitch1`.
- Alpha: preserve alpha; RGB-only corruption by default.
- Mask visualization is not exposed in the UI.
- Mode selection: checkboxes, allowing several effects to be combined.
- Advanced controls: hidden/reduced; derive intensity, block size, and mask parameters from the seed.
- Creative priority: expressive visual variety over strict simulation fidelity.
- Seed default: randomized once when the effect dialog opens.
