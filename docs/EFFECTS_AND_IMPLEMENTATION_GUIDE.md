# How do the effects work? 

This guide explains what glitch1 does, why each effect is interesting from a computer science perspective, and how the code works. No prior image processing knowledge is needed.

---

## Part 1: What Are Glitch Effects?

### What is a Glitch Effect?

A **glitch effect** is a visual corruption that mimics real computer errors. Instead of faithfully rendering an image, we intentionally distort it in ways that look like data got corrupted somewhere in the pipeline.

Examples of real glitches:
- **JPEG compression artifacts** when you save a photo at extremely low quality (blocks of color appear)
- **Channel misalignment** when data for RGB colors gets mixed up
- **Memory corruption** when a program reads from the wrong address and gets garbage data
- **Floating-point rounding errors** when math operations lose precision

In glitch1, we **simulate** these errors artistically. The result looks like the image got corrupted by a computer bug, but we have full control over it.

### Why This Is Interesting (Computer Science Perspective)

1. **Determinism**: In a normal image effect, randomness is chaotic. Here, we must ensure the **same seed produces identical output**, even when Paint.NET renders different tiles out of order. This requires careful coordinate-based hashing.

2. **Data Corruption Simulation**: We're simulating how real computers fail — but intentionally and artistically. This teaches about how fragile digital data is.

3. **Thread Safety**: Paint.NET can render different regions of an image simultaneously. Our code must produce identical results regardless of render order. This is a real-world parallel computing challenge.

4. **Performance**: We don't need to allocate memory for every pixel. Instead, we can must pre-compute surfaces and reuse them. This mirrors constraints in game engines and video processing.

---

## Part 2: The 11 Effects Explained

### Effect 1: Mismapped Channel

**What it does**: 
- Randomly swaps two RGB color channels so all reds become greens, greens become blues, etc.
- Uses seed-derived logic to pick which swap: R↔G, R↔B, or G↔B
- Every pixel gets the same swap applied uniformly
- Also applies a purely aesthetic transform that can stretch and rotate the image before the channel swap lands
- **Implementation**: For each pixel, picks swap variant via `uint variant = Hash(seed, 0, 0, 301) % 3`, then applies the seed-derived stretch/rotate transform and returns `ColorBgra(input.G, input.R, input.B, input.A)` (swapping R and G)
- **Visual result**: Red areas turn cyan, green areas turn magenta, blue areas turn yellow, and the image may also look stretched or rotated for extra visual variation. The entire color palette inverts in a predictable way based on the swap choice.

**The bug it mimics**: 
- Hardware reads R data but interprets it as G
- Software misaligns color channel buffers
- RGBA data gets read as BGRA (byte order confusion)

**CS Concept**: **Type reinterpretation**. This teaches about how the same bytes can mean different things depending on context.

---

### Effect 2: Data Type Confusion

**What it does**:
- Applies seed-driven color channel rotation/reinterpretation to simulate misaligned buffer reads
- Treats BGRA bytes as if they were read at the wrong offset: RBGA, GBAR, ABGR, etc.
- Each pixel's transformation is deterministic from the seed
- **Implementation**: Picks a rotation mode `int rotation = Hash(seed, 0, 0, 235) % 4`, then reconstructs BGRA in different orders (e.g., if rotation=1: moves alpha to position 0, shifts RGB down)
- **Visual result**: Colors become unsaturated or shift unpredictably—reds might become brownish, colors lose brightness, or invert depending on which bytes swap positions

**The bug it mimics**:
- C code reads a `uint32` (4-byte integer) but interprets it as a `float`
- Memory buffer gets read as signed instead of unsigned
- Endianness mismatch (Intel vs ARM byte order)

**CS Concept**: **Memory representation**. Different data types use the same bytes but interpret them differently.

---

### Effect 3: Memory Tear

**What it does**:
- Displaces horizontal rows of pixels to create "tear" lines (interlacing effect)
- For each row, computes a seed-driven vertical offset and replaces current row with row from different position
- Uses clamped sampling so displacement wraps at edges
- **Implementation**: For row `y`, calculates `offset = Hash(seed, blockX, y/blockSize, salt) % 8`, then samples from `displaced_y = Clamp(y + offset, 0, height-1)`. If intensity threshold passes, uses displaced row instead
- **Visual result**: Horizontal stripes/banding appear where rows shift up/down, creating interlaced glitch lines or scan-line artifacts

**The bug it mimics**:
- Video memory is being updated while CPU is reading it (screen tearing)
- Two threads write to adjacent rows simultaneously (race condition)
- Memory stride is wrong (image buffer skips data between rows)

**CS Concept**: **Race conditions** and **memory layout**. Shows what happens when multiple processes access shared memory without synchronization.

---

### Effect 4: Lack Of Clamping

**What it does**:
- Applies coordinate-based offset sampling to distort image spatially
- Reads pixels from offset positions: instead of (x, y), reads from (x + offset_x, y + offset_y)
- When the offset goes beyond bounds (0-100% of the image area), it wraps or repeats edge pixels instead of reading garbage (simulating out-of-bounds memory read wrapping)
- **Implementation**: For each pixel, computes `offsetX = Hash(seed, x/16, y/16, 460) % 20`, then samples `SampleClamped(src, x + offsetX, y + offsetY)` which repeats edge pixels instead of reading garbage
- **Visual result**: Edges appear smeared or displaced, colors shift positionally creating warping/displacement glitch especially visible at image boundaries

**The bug it mimics**:
- Code tries to read pixel at (x+100, y+100) but image is only 64×64
- No bounds check: reads garbage memory beyond the buffer
- Wrap-around behavior where coordinates loop to 0 (modulo wrapping)

**CS Concept**: **Buffer overflow** and **memory safety**. A classic security vulnerability.

---

### Effect 5: Floating Point Error

**What it does**:
- Randomly selects individual color channels (R, G, B, or HSV: Hue/Saturation/Value) and applies floating-point precision loss
- For RGB: divides channel by ~3, multiplies back: `float fValue = (float)channel / 3.0f; corrupted = (byte)(fValue * 3.0f);` — loses precision in round-trip
- For HSV: converts RGB to HSV color space, corrupts HSV channels, converts back to RGB
- **Implementation**: Picks channel deterministically `int channel = Hash(seed, x, y, 505) % 6`, then applies precision loss if intensity threshold passes
- **Visual result**: Speckled color noise where different pixels lose different channels—some pixels lose red (become cyan), others lose green (become magenta), others lose blue (become yellow). Creates noisy, slightly desaturated appearance.

**The bug it mimics**:
- Each math operation loses a tiny bit of precision
- Errors compound as more operations are chained
- Different CPU architectures (Intel vs ARM) or GPU implementations compute slightly differently

**CS Concept**: **Floating point representation**. Computers can't represent all decimals exactly—they use fixed-precision formats that lose data.

---

### Effect 6: Packet Loss

**What it does**:
- Identifies rectangular blocks of pixels (seed-derived grid) and corrupts them
- For each block, decides if it's "lost" based on seed and intensity threshold
- Lost blocks are replaced with distorted data (simplified colors, repeated colors, or averaged colors)
- **Implementation**: Divides into blocks (e.g., 8×8 or 16×16), then for each block checks `if (Hash(seed, blockX, blockY, 500) % 100 < intensity * 100)`, then fills block with GetDistortedVersion()
- **Visual result**: Square/rectangular glitch blocks appear throughout image showing color banding, dark areas, or simplified flat colors (like missing sectors of data)

**The bug it mimics**:
- Network packet drops while streaming video
- Disk read error corrupts a sector
- Cosmic ray flips a bit in memory (real hardware failure)

**CS Concept**: **Data integrity** and **error handling**. Important in networking (UDP packet loss) and storage (sector errors).

---

### Effect 7: Compression Artifacts

**What it does**:
- Performs actual JPEG encode-decode round-trip at extremely low quality (quality 2-12 instead of standard 75-95)
- Creates authentic JPEG compression artifacts, not approximated ones
- **Implementation**:
  1. Convert source surface to Bitmap
  2. Encode to JPEG bytes with seed-derived quality: `quality = 2 + (Hash(seed, 0, 0, 127) % 10)`
  3. Decode JPEG bytes back to Surface (cached for performance)
  4. For each pixel, apply delta: `result = current + (decodedSurface - original)`
- **Visual result**: Visible 8×8 pixel blocks with color banding (JPEG's block size), loss of fine detail, posterized colors, fuzzy gradients replaced with flat bands

**The bug it mimics**:
- JPEG's lossy compression creates block artifacts
- Very old or damaged image files
- Extreme compression for low bandwidth (video streaming)

**CS Concept**: **Lossy compression**. JPEG divides images into 8×8 blocks and throws away high-frequency detail (colors that vary rapidly) to save space.

---

### Effect 8: JPEG Binary Corruption

**What it does**:
- Encodes image as JPEG, then corrupts the JPEG binary data before decoding
- Specifically targets entropy data (compressed pixel data), leaving headers intact
- Applies multiple corruption modes: bit flips, byte insertions, byte deletions, byte relocations
- **Implementation**:
  1. Encode source to JPEG bytes
  2. Find JPEG entropy start/end markers (scanning for FF D8 and FF D9)
  3. Apply damage: random bit flips, insert/delete/relocate chunks within entropy data
  4. Decode corrupted bytes back to image (with error recovery)
- **Visual result**: Heavy color distortion, color shifts, broken scanlines, garbage colors where JPEG decoder tries to interpret corrupted data

**The bug it mimics**:
- Damaged JPEG file (corrupted hard drive)
- Network transmission error corrupts bits
- Incomplete/truncated JPEG download

**CS Concept**: **Error resilience**. JPEG decoders try to recover from corrupt data but fail progressively as damage increases.

---

### Effect 9: PNG Binary Corruption

**What it does**:
- Encodes image as PNG, then corrupts PNG binary data before decoding
- Targets only IDAT chunks (compressed image data), leaving metadata and CRC checksums intact
- Applies gentler corruption modes than JPEG: bit flips, low-bit noise, small deletions, byte swaps
- **Implementation**: Similar to JPEG but finds PNG IDAT chunk ranges, applies more conservative damage (doesn't delete as much to preserve PNG structure)
- **Visual result**: Color noise, pixel shift artifacts, but generally more subtle than JPEG since PNG uses lossless compression (corruption is more visible as pure noise/garbage)

**The bug it mimics**:
- Damaged PNG file
- Network transmission error
- IDAT chunk corruption

**CS Concept**: **Error detection**. PNG includes CRC checksums that fail on corruption, but decoder may still try to display partial data.

---

### Effect 10: GIF Binary Corruption

**What it does**:
- Encodes image as GIF, then corrupts GIF binary data before decoding
- Targets GIF image data while preserving color palette and headers
- Uses similar corruption strategies to PNG: bit flips, byte modifications
- **Implementation**: Finds GIF image descriptor, applies damage within image data blocks
- **Visual result**: Indexed color palette errors—pixels map to wrong palette entries, creating color shift artifacts, palette pollution effects

**The bug it mimics**:
- Corrupted GIF file
- Indexed color palette data error

**CS Concept**: **Indexed color palettes**. GIF stores a color table (palette) of 256 colors, and pixels reference indices into it. Corrupting data means wrong indices → wrong colors.

---

### Effect 11: Pre-Rotation (8 modes)

**What it does**:
- Rotates or flips the entire source image based on seed before applying other effects
- Seed determines which of 8 transformations to use
- Adds variation so same seed + same settings produce different results on rotated images
- **Implementation**: Picks mode `int mode = Hash(seed, 0, 0, 180) % 8`, then rotates: 0=none, 1=90°, 2=180°, 3=270°, 4=flip_h, 5=flip_v, 6=flip_both, 7=transpose
- **Visual result**: Image gets rotated/flipped BEFORE effects apply, so effect patterns follow the pre-rotated image orientation

**The bug it mimics**: No specific bug—adds variation and deterministic symmetry breaking

**CS Concept**: **Affine transformations**. Shows how rotation/flip matrices work in image processing.

---

## Part 3: How The Code Works

### The Big Picture

```
User clicks Effect → glitch1
        ↓
Paint.NET calls Render(dst, src, Rectangle)
        ↓
Render() checks cache (Did we compute this before?)
        ↓
If not cached:
  - Generate seed-derived effect parameters
  - Create rotated version of source image
  - Loop through all active effects (apply each one)
  - Cache the result
        ↓
Copy the result region to output
        ↓
Paint.NET displays the result
```

### Code Section 1: Deterministic Hashing

**Problem**: We need randomness, but `System.Random()` creates different results on different thread orders.

**Solution**: Use coordinate-based hashing.

```csharp
// Every pixel (x, y) with the same seed gets the same "random" value
uint Hash(uint seed, uint x, uint y, uint salt)
{
    // Start with seed
    uint h = seed;
    
    // Mix in coordinates (order matters)
    h ^= x;
    h = RotateLeft(h, 13);
    h ^= y;
    h = RotateLeft(h, 17);
    
    // Mix in salt (for different "random" values at same pixel)
    h ^= salt;
    h = RotateLeft(h, 19);
    
    // Return avalanche-hashed result
    return Avalanche(h);
}

// This produces the same output every time for the same (seed, x, y, salt)
// Perfect! Thread-safe and deterministic.
```

**Why this works**: The hash function is a **pure function** (same input → same output). No side effects, no global state.

### Code Section 2: The Render Function

```csharp
void Render(Surface dst, Surface src, Rectangle rect)
{
    // Get current UI settings
    int seedValue = seed;  // User-controlled seed
    bool jpegEnabled = compressionArtifacts;
    
    // Check if we've cached the result for this seed/image size
    if (cachedEffectOutput != null && 
        cachedSeedForCodecs == seedValue &&
        cachedSourceWidth == src.Width)
    {
        // Reuse cached result!
        CopyRegionToOutput(cachedEffectOutput, dst, rect);
        return;
    }
    
    // Not cached. Compute effects.
    Surface result = new Surface(src.Width, src.Height);
    
    // Apply each active effect to every pixel
    for (int y = 0; y < src.Height; y++)
    {
        for (int x = 0; x < src.Width; x++)
        {
            ColorBgra pixelIn = src[x, y];
            ColorBgra pixelOut = pixelIn;
            
            // Check which effects to apply (seed-driven selection)
            int[] effectOrder = GetEffectOrder(seedValue, ...);
            
            // Apply each effect in order
            foreach (int effectId in effectOrder)
            {
                pixelOut = ApplyEffectById(effectId, pixelOut, x, y, seedValue);
            }
            
            result[x, y] = pixelOut;
        }
    }
    
    // Cache for next render
    cachedEffectOutput = result;
    cachedSeedForCodecs = seedValue;
    
    // Copy to output
    for (int y = rect.Top; y < rect.Bottom; y++)
        for (int x = rect.Left; x < rect.Right; x++)
            dst[x, y] = result[x, y];
}
```

**Key insight**: We compute the ENTIRE image once, then cache it. If Paint.NET asks us to render the same region again (it often does), we just copy from cache.

### Code Section 3: Effect Application

```csharp
ColorBgra ApplyEffectById(int effectId, ColorBgra input, int x, int y, int seed)
{
    // Each effect gets coordinate-based randomness
    
    if (effectId == 1)  // Mismapped Channel
    {
        // Which swap variant for this seed?
        int variant = Hash(seed, 0, 0, 301) % 3;  // 0, 1, or 2
        
        if (variant == 0) // Swap R and G
            return new ColorBgra(input.G, input.R, input.B, input.A);
        else if (variant == 1) // Swap R and B
            return new ColorBgra(input.B, input.G, input.R, input.A);
        else // Swap G and B
            return new ColorBgra(input.R, input.B, input.G, input.A);
    }
    else if (effectId == 2)  // Floating Point Error
    {
        // Accumulate small rounding errors
        double errorFactor = 0.99 + (Random01(seed, x, y, 401) * 0.02);
        
        byte newR = ClampByte((int)(input.R * errorFactor));
        byte newG = ClampByte((int)(input.G * errorFactor));
        byte newB = ClampByte((int)(input.B * errorFactor));
        
        return new ColorBgra(newB, newG, newR, input.A);
    }
    
    // ... etc for other effects
}
```

**Key insight**: Each effect gets the **same input** (the pixel), but uses **coordinate + effect-specific salt** to generate "random" parameters. Since the coordinates are deterministic (always processing the same pixel at the same position), the output is deterministic.

### Code Section 4: Alpha Preservation

All effects MUST preserve alpha:

```csharp
ColorBgra BlendPreserveAlpha(ColorBgra original, ColorBgra corrupted, double amount)
{
    // Blend RGB but keep original alpha
    byte r = ClampByte((int)(original.R * (1-amount) + corrupted.R * amount));
    byte g = ClampByte((int)(original.G * (1-amount) + corrupted.G * amount));
    byte b = ClampByte((int)(original.B * (1-amount) + corrupted.B * amount));
    
    byte a = original.A;  // ← Alpha NEVER changes
    
    return new ColorBgra(b, g, r, a);
}
```

**Why?**: If an effect accidentally corrupts alpha, transparent areas become opaque or vice versa. That's not a glitch effect, it's a bug. We want only RGB glitches.

### Code Section 5: Codec Operations (JPEG)

This is where it gets expensive. We actually compress and decompress:

```csharp
void PrepareJpegSurfaces()
{
    // Step 1: Encode source image to JPEG format in memory
    MemoryStream jpegBytes = new MemoryStream();
    using (var jpegCodec = GetJpegCodec())
    {
        jpegCodec.Save(src, jpegBytes, 5);  // Quality = 5 (very low!)
    }
    
    // Step 2: Corrupt the JPEG bytes
    byte[] data = jpegBytes.ToArray();
    for (int i = 0; i < data.Length; i++)
    {
        if (Hash(seed, i, 0, 501) % 20 == 0)  // Every ~20th byte
            data[i] ^= 0xFF;  // Flip all bits
    }
    
    // Step 3: Decode corrupted JPEG back to image
    MemoryStream corruptedStream = new MemoryStream(data);
    Surface jpegSurface = new Surface();
    jpegCodec.Load(corruptedStream, jpegSurface);
    
    return jpegSurface;
}
```

**Why this is slow**: 
- JPEG compression/decompression is expensive (lots of math)
- Happens for EVERY render pass if enabled
- That's why we cache it!

**Why it's authentic**: Real JPEG compression artifacts. Not faked.

### Code Section 6: The Mask (Optional Spatial Control)

Some effects use a mask to determine WHERE to apply corruption:

```csharp
double GetMaskValue(int x, int y, int seed)
{
    // Create a wave pattern using sine waves
    double wave1 = Math.Sin((x * 0.01) + (seed * 0.1)) * 0.5 + 0.5;
    double wave2 = Math.Cos((y * 0.01) + (seed * 0.2)) * 0.5 + 0.5;
    
    // Add seeded turbulence
    uint turbulence = Hash(seed, x / 16, y / 16, 601);
    double noise = (double)turbulence / uint.MaxValue;
    
    // Combine
    double mask = (wave1 + wave2 + noise) / 3.0;
    
    // Clamp to [0, 1]
    return Math.Max(0, Math.Min(1, mask));
}

// Usage:
double intensity = GetMaskValue(x, y, seed);
// intensity = 0.0 → No effect
// intensity = 1.0 → Full effect
// intensity = 0.5 → Half effect
```

**Result**: The glitch effect appears in waves/clouds instead of uniformly everywhere.

---

## Part 4: Why This Design Matters

### Challenge 1: Determinism Under Parallelism

**The Problem**: Paint.NET renders different image tiles simultaneously on different CPU cores. If we use global random state:

```csharp
// BAD ❌
Random rand = new Random(seed);
for (int i = 0; i < numPixels; i++)
{
    effects[i] = rand.Next();  // WRONG: depends on order tiles are processed
}
```

**The Solution**: Coordinate-based hashing:

```csharp
// GOOD ✅
for (int i = 0; i < numPixels; i++)
{
    effects[i] = Hash(seed, x, y, salt);  // CORRECT: same output always
}
```

Paint.NET can render tiles in any order. Result is identical.

### Challenge 2: Performance vs Memory

**The Problem**: A 1920×1080 image has 2 million pixels. We can't allocate a temporary surface per effect.

**The Solution**: Pre-allocate, reuse, cache:

```csharp
// Allocate once, before loop
Surface cachedJpegSurface = null;

void Render()
{
    // Check cache first
    if (cachedJpegSurface != null && CacheIsValid())
    {
        // Reuse cached surface
        CopyFromCache();
        return;
    }
    
    // Compute only if needed
    cachedJpegSurface = ComputeJpegSurface();
}
```

**Result**: First render is slow, subsequent renders are fast.

### Challenge 3: Alpha Preservation

**The Problem**: If an effect corrupts alpha, transparency breaks.

**The Solution**: Explicit preservation:

```csharp
// Every effect must do this
byte alphaOriginal = source.A;
// ... do corruption ...
result.A = alphaOriginal;  // Restore
```

---

## Part 5: Learning Path

If you want to understand the code:

1. **Start here**: Read the `Render()` function (core logic)
2. **Then**: Read `GetEffectOrder()` (how effects are selected)
3. **Then**: Pick one simple effect and trace through `ApplyEffectById()`
4. **Then**: Read `Hash()` and understand deterministic randomness
5. **Advanced**: Read `PrepareJpegSurfaces()` to see codec operations

---

## Part 6: Common Questions & Answers

**Q: Why not use `System.Random()`?**  
A: Thread-unsafe and order-dependent. Two renders of the same image on different core orders would produce different results.

**Q: Why encode/decode JPEG instead of faking the artifacts?**  
A: Authenticity. Real JPEG produces unique artifacts for each image. Faking it looks artificial.

**Q: Why does alpha never change?**  
A: Transparency is a layer property. Corrupting it breaks composition. Glitches should corrupt color, not transparency.

**Q: How do effects compose?**  
A: Each effect reads the output of the previous effect, not the original. So Mismapped Channel → Memory Tear means the tears happen to already-misma mapped colors.

**Q: Why cache surfaces?**  
A: JPEG/PNG/GIF encoding is expensive (~50ms per 1920×1080). Caching lets Paint.NET re-render the same region instantly.

---

## References

- Paint.NET CodeLab API: [paint.net documentation](https://www.getpaint.net/)
- Project specification: See [GLITCH_EFFECT_PLUGIN_PLAN.md](GLITCH_EFFECT_PLUGIN_PLAN.md)
- Source code: [src/glitch1.codelab.cs](../src/glitch1.codelab.cs)
- Performance analysis: See [IMPLEMENTATION_CHECKLIST.md](IMPLEMENTATION_CHECKLIST.md) and [PERFORMANCE_TEST_PLAN.md](PERFORMANCE_TEST_PLAN.md)

