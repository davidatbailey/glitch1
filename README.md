# glitch1 — Paint.NET Deterministic Glitch Effect Plugin

A Paint.NET CodeLab effect that applies seed-driven data-corruption visual glitches inspired by real computer bugs: JPEG compression failures, channel misalignment, floating-point errors, buffer overflows, and more.

## How to use

1. Copy `glitch1.dll` from the `bin` folder into your Paint.NET installation folder e.g. `C:\Program Files\Paint.NET\Effects`
2. Launch **Paint.NET** and open an image
3. If the image is large, potentially resize it smaller, this improves the performance of the plugin.
4. Select **Effects > Distort > Glitch1**

## Build

1. Ensure you have the CodeLab plugin installed on Paint.NET
2. Open **Paint.NET**
3. Open **Effects > Advanced > CodeLab...**
4. Paste the contents of [src/glitch1.codelab.cs](src/glitch1.codelab.cs)
5. Click the **Preview Effect** icon (Ctrl+P) to test the code (this runs in debug mode and so is slower)
6. Click **Compile**
7. Save as `glitch1.dll`
8. Copy the dll from the `bin` folder into your Paint.NET installation folder e.g. `C:\Program Files\Paint.NET\Effects`
7. Follow the usage instructions above

## Glitch effects

|Effect            ||
|---|---|
|Original |![img](/img/tortoise_original.jpg)|
|Mismapped Channel |![img](/img/tortoise_mismappedchannel.jpg)|
|Data Type Confusion |![img](/img/tortoise_datatypeconfusion.jpg)|
|Memory Tear |![img](/img/tortoise_memorytear.jpg)|
|Lack Of Clamping |![img](/img/tortoise_lackofclamping.jpg)|
|Floating Point Error |![img](/img/tortoise_floatingpointerror.jpg)|
|Packet Loss |![img](/img/tortoise_packetloss.jpg)|
|Compression Artifacts |![img](/img/tortoise_compressionartifacts.jpg)|
|JPEG Binary Corruption |![img](/img/tortoise_jpegbinarycorruption.jpg)|
|PNG Binary Corruption |![img](/img/tortoise_pngbinarycorruption.jpg)|
|GIF Binary Corruption |![img](/img/tortoise_gifbinarycorruption.jpg)|
|Data Type Confusion & Compression Artifacts together|![img](/img/tortoise_datatypeandcompression.jpg)|
|All effects seed 1|![img](/img/tortoise_all.jpg)|
|All effects seed 2|![img](/img/tortoise_all2.jpg)|

## Features

**11 Composable Effects**:
- Mismapped Channel, Data Type Confusion, Memory Tear, Lack Of Clamping
- Floating Point Error, Packet Loss, Compression Artifacts
- JPEG/PNG/GIF Binary Corruption, Pre-Rotation (8 modes)

**Key Properties**:
- ✅ **Deterministic**: Same seed + settings = identical output every time
- ✅ **Thread-safe**: Paint.NET tile rendering produces identical results
- ✅ **Performant**: Pre-allocated surfaces, coordinate-based randomness
- ✅ **Alpha-safe**: Preserves transparency; only RGB corrupts

## Usage controls
- **Seed** (1–10000): Each effect is randomised based on the seed value
- **Effect Checkboxes**: Enable/disable individual glitches
- The source image is randomly flipped and rotated
- The sequence of applying effects is also random
- The same seed will produce identical output on re-render

![img](/img/glitch1_ui.jpg)

## Documentation

- [EFFECTS_AND_IMPLEMENTATION_GUIDE.md](docs/EFFECTS_AND_IMPLEMENTATION_GUIDE.md) — **Start here**: Beginner-friendly explanation of all effects, design concepts, and code
- [AGENTS.md](AGENTS.md) — Code patterns and development guide
- [docs/IMPLEMENTATION_CHECKLIST.md](docs/IMPLEMENTATION_CHECKLIST.md) — Progress tracking

## License

See [LICENSE](LICENSE)
