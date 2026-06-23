// glitch1 - Paint.NET CodeLab ClassicEffect script
//
// This script is structured as a coding exemplar: deterministic coordinate
// hashing, source-only sampling, alpha preservation, and minimal per-pixel work.
// In CodeLab, use/accept the ClassicEffect project type for this Surface API.

#region UICode
IntSliderControl seed = 1; // [1,10000] Seed number
CheckboxControl mismappedChannel = true; // Mismapped Channel
CheckboxControl dataTypeConfusion = false; // Data Type Confusion
CheckboxControl memoryTear = false; // Memory Tear
CheckboxControl lackOfClamping = false; // Lack Of Clamping
CheckboxControl floatingPointError = false; // Floating Point Error
CheckboxControl packetLoss = false; // Packet Loss
CheckboxControl compressionArtifacts = false; // Compression Artifacts
CheckboxControl jpegBinaryCorruption = false; // JPEG Binary Corruption
CheckboxControl pngBinaryCorruption = false; // PNG Binary Corruption
CheckboxControl gifBinaryCorruption = false; // GIF Binary Corruption
#endregion

Surface cachedEffectOutput = null;
Surface cachedSourceSurface = null;
Surface cachedPngDamageSurface = null;
Surface cachedGifDamageSurface = null;
int cachedSourceWidth = -1;
int cachedSourceHeight = -1;
int cachedRotationMode = 0;
int cachedSettingsKey = 0;
int cachedSeedForCodecs = -1;

// PHASE 2.A: Coefficient caching (cache per seed to reuse across Paint.NET tile renders)
int cachedCoefficientsKey = -1;
int cachedBlockSize = -1;
double cachedIntensity = -1.0;
int cachedJpegQuality = -1;
int cachedVolatileFrameSalt = 0;

void Render(Surface dst, Surface src, Rectangle rect)
{
    int seedValue = seed;
    bool mismappedChannelEnabled = mismappedChannel;
    bool compressionArtifactsEnabled = compressionArtifacts;
    bool packetLossEnabled = packetLoss;
    bool dataTypeConfusionEnabled = dataTypeConfusion;
    bool lackOfClampingEnabled = lackOfClamping;
    bool memoryTearEnabled = memoryTear;
    bool jpegBinaryCorruptionEnabled = jpegBinaryCorruption;
    bool pngBinaryCorruptionEnabled = pngBinaryCorruption;
    bool gifBinaryCorruptionEnabled = gifBinaryCorruption;
    bool floatingPointErrorEnabled = floatingPointError;
    
    int rotationMode = 0;
    Surface rotatedSrc = src;
    int rotationModeValue = (int)(Hash(seedValue, 0, 0, 241) % 8u);
    rotationMode = rotationModeValue;
    
    // PHASE 2.A: Get or compute coefficients (cached per seed) - MUST be before ComputeSettingsKey
    GetEffectCoefficients(seedValue, memoryTearEnabled, out int blockSize, out double intensity, out int jpegQuality, out int volatileFrameSalt);
    
    int settingsKey = ComputeSettingsKey(
        seedValue,
        mismappedChannelEnabled,
        compressionArtifactsEnabled,
        packetLossEnabled,
        dataTypeConfusionEnabled,
        lackOfClampingEnabled,
        memoryTearEnabled,
        jpegBinaryCorruptionEnabled,
        pngBinaryCorruptionEnabled,
        gifBinaryCorruptionEnabled,
        floatingPointErrorEnabled,
        rotationMode,
        volatileFrameSalt);

    bool rebuildCache =
        cachedEffectOutput == null ||
        cachedSourceSurface == null ||
        !object.ReferenceEquals(cachedSourceSurface, src) ||
        cachedSourceWidth != src.Width ||
        cachedSourceHeight != src.Height ||
        cachedRotationMode != rotationMode ||
        cachedSettingsKey != settingsKey;

    bool invalidateCodecCache = rebuildCache || cachedSeedForCodecs != seedValue;

    if (rebuildCache)
    {
        // Invalidate codec caches when effect settings change
        if (invalidateCodecCache)
        {
            cachedPngDamageSurface = null;
            cachedGifDamageSurface = null;
            cachedSeedForCodecs = seedValue;
        }

        rotatedSrc = ApplyPreRotation(src, rotationMode);

    bool stackEffects = true;  // Always stack effects for consistent composition
    
    int[] effectOrder = GetEffectOrder(
        seedValue,
        stackEffects,
        compressionArtifactsEnabled,
        packetLossEnabled,
        dataTypeConfusionEnabled,
        lackOfClampingEnabled,
        memoryTearEnabled,
        mismappedChannelEnabled,
        jpegBinaryCorruptionEnabled,
        pngBinaryCorruptionEnabled,
        gifBinaryCorruptionEnabled,
        floatingPointErrorEnabled);

        // Apply each enabled effect as a full-image pass in seeded order.
        Surface finalSurface = rotatedSrc;
        if (effectOrder.Length > 0)
        {
            Surface scratchA = new Surface(rotatedSrc.Width, rotatedSrc.Height);
            Surface scratchB = new Surface(rotatedSrc.Width, rotatedSrc.Height);
            Surface current = rotatedSrc;
            Surface output = scratchA;

            for (int i = 0; i < effectOrder.Length; i++)
            {
                ApplyEffectPass(effectOrder[i], current, output, seedValue, blockSize, intensity, jpegQuality, volatileFrameSalt);

                // Swap buffers for the next pass.
                Surface temp = current;
                current = output;
                output = (temp == scratchA) ? scratchB : scratchA;
            }

            finalSurface = current;
        }

        cachedEffectOutput = finalSurface;
        cachedSourceSurface = src;
        cachedSourceWidth = src.Width;
        cachedSourceHeight = src.Height;
        cachedRotationMode = rotationMode;
        cachedSettingsKey = settingsKey;
    }

    // Only write the requested tile region; cachedEffectOutput is full-frame.
    for (int y = rect.Top; y < rect.Bottom; y++)
    {
        for (int x = rect.Left; x < rect.Right; x++)
        {
            int rotX, rotY;
            int rotW = (rotationMode / 2 == 1 || rotationMode / 2 == 3) ? src.Height : src.Width;
            int rotH = (rotationMode / 2 == 1 || rotationMode / 2 == 3) ? src.Width : src.Height;
            ReversePreRotationCoords(x, y, rotW, rotH, src.Width, src.Height, rotationMode, out rotX, out rotY);

            if (rotX >= 0 && rotX < cachedEffectOutput.Width && rotY >= 0 && rotY < cachedEffectOutput.Height)
            {
                dst[x, y] = cachedEffectOutput[rotX, rotY];
            }
        }
    }
}



void PrepareJpegSurfaces(
    Surface src,
    int quality,
    int seed,
    bool compressionArtifactsEnabled,
    out Surface jpegSurface)
{
    jpegSurface = null;

    if (!compressionArtifactsEnabled)
    {
        return;
    }

    byte[] jpegBytes = EncodeSurfaceAsJpegBytes(src, quality);
    if (jpegBytes == null)
    {
        jpegSurface = CopySourceToSurface(src);
        return;
    }

    Surface sourceFallback = CopySourceToSurface(src);
    jpegSurface = DecodeJpegBytesToSurface(jpegBytes, src.Width, src.Height, sourceFallback);
}

int[] GetEffectOrder(
    int seed,
    bool stackEffects,
    bool compressionArtifactsEnabled,
    bool packetLossEnabled,
    bool dataTypeConfusionEnabled,
    bool lackOfClampingEnabled,
    bool memoryTearEnabled,
    bool mismappedChannelEnabled,
    bool jpegBinaryCorruptionEnabled,
    bool pngBinaryCorruptionEnabled,
    bool gifBinaryCorruptionEnabled,
    bool floatingPointErrorEnabled)
{
    int count = 0;
    if (compressionArtifactsEnabled) count++;
    if (packetLossEnabled) count++;
    if (dataTypeConfusionEnabled) count++;
    if (lackOfClampingEnabled) count++;
    if (memoryTearEnabled) count++;
    if (mismappedChannelEnabled) count++;
    if (jpegBinaryCorruptionEnabled) count++;
    if (pngBinaryCorruptionEnabled) count++;
    if (gifBinaryCorruptionEnabled) count++;
    if (floatingPointErrorEnabled) count++;

    int[] order = new int[count];
    int index = 0;
    if (compressionArtifactsEnabled) order[index++] = 0;
    if (packetLossEnabled) order[index++] = 1;
    if (dataTypeConfusionEnabled) order[index++] = 2;
    if (lackOfClampingEnabled) order[index++] = 3;
    if (memoryTearEnabled) order[index++] = 4;
    if (mismappedChannelEnabled) order[index++] = 5;
    if (jpegBinaryCorruptionEnabled) order[index++] = 6;
    if (pngBinaryCorruptionEnabled) order[index++] = 7;
    if (gifBinaryCorruptionEnabled) order[index++] = 8;
    if (floatingPointErrorEnabled) order[index++] = 9;

    if (!stackEffects)
    {
        return order;
    }

    for (int i = 0; i < order.Length - 1; i++)
    {
        int swapIndex = i + (int)(Hash(seed, i, 0, 227) % (uint)(order.Length - i));
        int temp = order[i];
        order[i] = order[swapIndex];
        order[swapIndex] = temp;
    }

    return order;
}

// PHASE 2.A: Cache coefficient computation (blockSize, intensity, jpegQuality, volatileFrameSalt)
// Reuses cached values across multiple Paint.NET tile renders with the same seed
void GetEffectCoefficients(int seed, bool memoryTearEnabled, out int blockSize, out double intensity, out int jpegQuality, out int volatileFrameSalt)
{
    if (cachedCoefficientsKey == seed)
    {
        blockSize = cachedBlockSize;
        intensity = cachedIntensity;
        jpegQuality = cachedJpegQuality;
        volatileFrameSalt = cachedVolatileFrameSalt;
        return;
    }
    
    intensity = 0.45 + (Random01(seed, 0, 0, 101) * 0.45);
    blockSize = 4 + ((int)(Hash(seed, 0, 0, 103) % 8u) * 2);
    jpegQuality = GetJpegQuality(seed, intensity);
    volatileFrameSalt = memoryTearEnabled ? (System.Environment.TickCount / 120) : 0;
    
    cachedCoefficientsKey = seed;
    cachedBlockSize = blockSize;
    cachedIntensity = intensity;
    cachedJpegQuality = jpegQuality;
    cachedVolatileFrameSalt = volatileFrameSalt;
}

void ApplyEffectPass(int effectId, Surface input, Surface output, int seed, int blockSize, double intensity, int jpegQuality, int volatileFrameSalt)
{
    if (input == null || output == null)
    {
        return;
    }

    if (effectId == 0)
    {
        Surface jpegPass;
        PrepareJpegSurfaces(input, jpegQuality, seed, true, out jpegPass);
        ReplaceRgbPreserveAlphaSurfaceInto(input, jpegPass, output);
        return;
    }

    if (effectId == 6)
    {
        Surface damagedJpeg = CreateJpegBinaryDamageSurface(input, 10, seed, 0.5);
        ReplaceRgbPreserveAlphaSurfaceInto(input, damagedJpeg, output);
        return;
    }

    if (effectId == 7)
    {
        // Use cached PNG damage surface if available, otherwise compute and cache
        if (cachedPngDamageSurface == null)
        {
            cachedPngDamageSurface = CreatePngBinaryDamageSurface(input, seed, 0.5);
        }
        ReplaceRgbPreserveAlphaSurfaceInto(input, cachedPngDamageSurface, output);
        return;
    }

    if (effectId == 8)
    {
        // Use cached GIF damage surface if available, otherwise compute and cache
        if (cachedGifDamageSurface == null)
        {
            cachedGifDamageSurface = CreateGifBinaryDamageSurface(input, seed, 0.5);
        }
        ReplaceRgbPreserveAlphaSurfaceInto(input, cachedGifDamageSurface, output);
        return;
    }

    for (int y = 0; y < input.Height; y++)
    {
        for (int x = 0; x < input.Width; x++)
        {
            ColorBgra current = input[x, y];
            ColorBgra transformed = current;

            if (effectId == 1)
            {
                transformed = MissingPacketRows(input, x, y, seed, blockSize, intensity);
            }
            else if (effectId == 2)
            {
                transformed = ChannelSwapLab(current, seed, x, y);
            }
            else if (effectId == 3)
            {
                transformed = LackOfClamping(current, seed, x, y, intensity);
            }
            else if (effectId == 4)
            {
                transformed = MemoryTear(input, x, y, seed, intensity, volatileFrameSalt);
            }
            else if (effectId == 5)
            {
                transformed = MismappedChannel(input, current, x, y, seed);
            }
            else if (effectId == 9)
            {
                transformed = FloatingPointError(current, seed, x, y, intensity);
            }

            output[x, y] = transformed;
        }
    }
}

void ReplaceRgbPreserveAlphaSurfaceInto(Surface baseSurface, Surface effectSurface, Surface output)
{
    if (baseSurface == null || output == null)
    {
        return;
    }

    if (effectSurface == null)
    {
        for (int y = 0; y < baseSurface.Height; y++)
        {
            for (int x = 0; x < baseSurface.Width; x++)
            {
                output[x, y] = baseSurface[x, y];
            }
        }
        return;
    }

    for (int y = 0; y < baseSurface.Height; y++)
    {
        for (int x = 0; x < baseSurface.Width; x++)
        {
            ColorBgra basePixel = baseSurface[x, y];
            ColorBgra effectPixel = SampleClamped(effectSurface, x, y);
            output[x, y] = ColorBgra.FromBgra(effectPixel.B, effectPixel.G, effectPixel.R, basePixel.A);
        }
    }
}

ColorBgra ApplyEffectById(
    int effectId,
    ColorBgra result,
    Surface src,
    Surface jpegSurface,
    Surface damagedJpegSurface,
    Surface damagedPngSurface,
    Surface damagedGifSurface,
    Surface accumulatedResult,
    int x,
    int y,
    int seed,
    int blockSize,
    double intensity,
    double amount,
    bool compressionArtifactsEnabled,
    bool packetLossEnabled,
    bool dataTypeConfusionEnabled,
    bool lackOfClampingEnabled,
    bool memoryTearEnabled,
    bool mismappedChannelEnabled,
    bool jpegBinaryCorruptionEnabled,
    bool pngBinaryCorruptionEnabled,
    bool gifBinaryCorruptionEnabled,
    bool floatingPointErrorEnabled)
{
    if (effectId == 0 && compressionArtifactsEnabled)
    {
        return JpegBlockPanic(result, src, jpegSurface, x, y);
    }

    if (effectId == 1 && packetLossEnabled)
    {
        return MissingPacketRows(src, x, y, seed, blockSize, intensity);
    }

    if (effectId == 2 && dataTypeConfusionEnabled)
    {
        return ChannelSwapLab(result, seed, x, y);
    }

    if (effectId == 3 && lackOfClampingEnabled)
    {
        return LackOfClamping(result, seed, x, y, intensity);
    }

    if (effectId == 4 && memoryTearEnabled)
    {
        return MemoryTear(accumulatedResult, x, y, seed, intensity);
    }

    if (effectId == 5 && mismappedChannelEnabled)
    {
        return MismappedChannel(src, result, x, y, seed);
    }

    if (effectId == 6 && jpegBinaryCorruptionEnabled)
    {
        return JpegBinaryCorruption(result, src, damagedJpegSurface, x, y);
    }

    if (effectId == 7 && pngBinaryCorruptionEnabled)
    {
        return PngBinaryCorruption(result, src, damagedPngSurface, x, y);
    }

    if (effectId == 8 && gifBinaryCorruptionEnabled)
    {
        return GifBinaryCorruption(result, src, damagedGifSurface, x, y);
    }

    if (effectId == 9 && floatingPointErrorEnabled)
    {
        return FloatingPointError(result, seed, x, y, intensity);
    }

    return result;
}

ColorBgra JpegBlockPanic(ColorBgra current, Surface src, Surface jpegSurface, int x, int y)
{
    return ApplySurfaceDelta(current, src, jpegSurface, x, y);
}

ColorBgra JpegBinaryCorruption(ColorBgra current, Surface src, Surface damagedJpegSurface, int x, int y)
{
    return ApplySurfaceDelta(current, src, damagedJpegSurface, x, y);
}

ColorBgra PngBinaryCorruption(ColorBgra current, Surface src, Surface damagedPngSurface, int x, int y)
{
    return ApplySurfaceDelta(current, src, damagedPngSurface, x, y);
}

ColorBgra GifBinaryCorruption(ColorBgra current, Surface src, Surface damagedGifSurface, int x, int y)
{
    return ApplySurfaceDelta(current, src, damagedGifSurface, x, y);
}

ColorBgra FloatingPointError(ColorBgra current, int seed, int x, int y, double intensity)
{
    // PHASE 2.A: Early bailout - block-level skip to reduce hash calls
    // Check if entire 16x16 block is skipped (very fast, 1 hash call instead of 3 per pixel)
    int blockX = x / 16;
    int blockY = y / 16;
    uint blockSkip = Hash(seed, blockX, blockY, 500);
    double blockProb = (double)(blockSkip & 0xFF) / 255.0;
    if (blockProb > (intensity * 1.5))  // Block-level threshold
    {
        return current;
    }
    
    // Per-pixel probability check (intensity controls density)
    double pixelProb = Random01(seed, x, y, 504);
    if (pixelProb > intensity)
    {
        return current;
    }

    // Choose which channel (0=R, 1=G, 2=B, 3=H, 4=S, 5=V)
    int channelChoice = (int)(Hash(seed, x, y, 505) % 6u);
    
    ColorBgra result = current;
    
    if (channelChoice < 3)
    {
        // RGB channels: accumulation precision loss
        // Direct channel access without array allocation
        byte channelValue = (channelChoice == 0) ? current.B 
                          : (channelChoice == 1) ? current.G 
                          : current.R;
        
        // Simulate precision loss through float round-trip
        // Divide by value close to 3, multiply back - should be same but loses precision in float
        float fValue = (float)channelValue / 3.0f;
        float recovered = fValue * 3.0f;
        byte corrupted = (byte)(int)recovered;
        
        // Reconstruct with corrupted channel
        if (channelChoice == 0)
            result = ColorBgra.FromBgra(corrupted, current.G, current.R, current.A);
        else if (channelChoice == 1)
            result = ColorBgra.FromBgra(current.B, corrupted, current.R, current.A);
        else
            result = ColorBgra.FromBgra(current.B, current.G, corrupted, current.A);
    }
    else
    {
        // HSV channels - convert to HSV, corrupt, convert back
        // Using integer approximations for HSV
        int r = current.R;
        int g = current.G;
        int b = current.B;
        
        int maxC = r > g ? (r > b ? r : b) : (g > b ? g : b);
        int minC = r < g ? (r < b ? r : b) : (g < b ? g : b);
        
        if (channelChoice == 3)
        {
            // Corrupt Hue via precision loss
            int deltaC = maxC - minC;
            if (deltaC > 0)
            {
                float hue = 0.0f;
                if (maxC == r)
                    hue = (float)(g - b) / (float)deltaC;
                else if (maxC == g)
                    hue = 2.0f + (float)(b - r) / (float)deltaC;
                else
                    hue = 4.0f + (float)(r - g) / (float)deltaC;
                
                // Precision loss in hue calculation
                float corruptedHue = hue / 1.5f;
                corruptedHue = corruptedHue * 1.5f;
                
                // Approximate back to RGB (simplified)
                int corrupted = (int)(corruptedHue * 10) % 255;
                result = ColorBgra.FromBgra((byte)corrupted, current.G, current.R, current.A);
            }
        }
        else if (channelChoice == 4)
        {
            // Corrupt Saturation via precision loss
            if (maxC > 0)
            {
                float sat = (float)(maxC - minC) / (float)maxC;
                float corruptedSat = sat / 1.5f;
                corruptedSat = corruptedSat * 1.5f;
                
                int corrupted = (int)(corruptedSat * 255);
                result = ColorBgra.FromBgra(current.B, (byte)corrupted, current.R, current.A);
            }
        }
        else
        {
            // Corrupt Value via precision loss
            float val = (float)maxC / 255.0f;
            float corruptedVal = val / 1.5f;
            corruptedVal = corruptedVal * 1.5f;
            
            byte corrupted = (byte)(int)(corruptedVal * 255);
            result = ColorBgra.FromBgra(corrupted, current.G, current.R, current.A);
        }
    }
    
    return BlendPreserveAlpha(current, result, intensity);
}

ColorBgra ApplySurfaceDelta(ColorBgra current, Surface originalSurface, Surface damagedSurface, int x, int y)
{
    if (damagedSurface == null || originalSurface == null)
    {
        return current;
    }

    ColorBgra original = SampleClamped(originalSurface, x, y);
    ColorBgra damaged = SampleClamped(damagedSurface, x, y);

    int db = damaged.B - original.B;
    int dg = damaged.G - original.G;
    int dr = damaged.R - original.R;

    return ColorBgra.FromBgra(
        (byte)ClampByte(current.B + db),
        (byte)ClampByte(current.G + dg),
        (byte)ClampByte(current.R + dr),
        current.A);
}

int GetJpegQuality(int seed, double intensity)
{
    int seededQuality = 2 + (int)(Random01(seed, 0, 0, 127) * 10.0);
    int intensityDrop = (int)(intensity * 4.0);
    return ClampInt(seededQuality - intensityDrop, 2, 12);
}

Surface CreateJpegRoundTripSurface(Surface src, int quality)
{
    byte[] jpegBytes = EncodeSurfaceAsJpegBytes(src, quality);
    if (jpegBytes == null)
    {
        return CopySourceToSurface(src);
    }

    return DecodeJpegBytesToSurface(jpegBytes, src.Width, src.Height, CopySourceToSurface(src));
}

Surface CreateJpegBinaryDamageSurface(Surface src, int quality, int seed, double damageIntensity)
{
    byte[] jpegBytes = EncodeSurfaceAsJpegBytes(src, quality);
    if (jpegBytes == null)
    {
        return CopySourceToSurface(src);
    }

    byte[] damagedBytes = DamageJpegBytes(jpegBytes, seed, damageIntensity);
    Surface fallback = DecodeJpegBytesToSurface(jpegBytes, src.Width, src.Height, CopySourceToSurface(src));
    return DecodeJpegBytesToSurface(damagedBytes, src.Width, src.Height, fallback);
}

byte[] EncodeSurfaceAsJpegBytes(Surface src, int quality)
{
    using (System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(src.Width, src.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb))
    {
        for (int y = 0; y < src.Height; y++)
        {
            for (int x = 0; x < src.Width; x++)
            {
                ColorBgra c = src[x, y];
                bitmap.SetPixel(x, y, System.Drawing.Color.FromArgb(c.R, c.G, c.B));
            }
        }

        using (System.IO.MemoryStream stream = new System.IO.MemoryStream())
        {
            System.Drawing.Imaging.ImageCodecInfo codec = GetJpegCodec();
            if (codec == null)
            {
                return null;
            }

            using (System.Drawing.Imaging.EncoderParameters encoderParameters = new System.Drawing.Imaging.EncoderParameters(1))
            {
                encoderParameters.Param[0] = new System.Drawing.Imaging.EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)quality);
                bitmap.Save(stream, codec, encoderParameters);
            }

            return stream.ToArray();
        }
    }
}

Surface DecodeJpegBytesToSurface(byte[] jpegBytes, int width, int height, Surface fallback)
{
    try
    {
        using (System.IO.MemoryStream stream = new System.IO.MemoryStream(jpegBytes))
        {
            using (System.Drawing.Bitmap decoded = new System.Drawing.Bitmap(stream))
            {
                return CopyBitmapToSurface(decoded, width, height);
            }
        }
    }
    catch
    {
        return fallback;
    }
}

byte[] DamageJpegBytes(byte[] source, int seed, double damageIntensity)
{
    int dataStart = FindJpegEntropyStart(source);
    int dataEnd = FindJpegEntropyEnd(source);
    if (dataStart < 0 || dataEnd <= dataStart + 16)
    {
        return source;
    }

    // Incorporate intensity into randomness so patterns change with slider
    int intensitySalt = (int)(damageIntensity * 1000.0);
    
    int mode = (int)(Hash(seed, 0, intensitySalt, 181) % 4u);
    if (mode == 0)
    {
        byte[] damaged = CopyBytes(source);
        int edits = (int)((12 + (int)(Random01(seed, 0, intensitySalt, 183) * 36.0)) * damageIntensity);
        int span = dataEnd - dataStart;
        for (int i = 0; i < edits; i++)
        {
            int index = dataStart + (int)(Hash(seed, i, intensitySalt, 187) % (uint)span);
            if (damaged[index] != 255)
            {
                damaged[index] = (byte)(damaged[index] ^ (byte)(1 + (Hash(seed, i, intensitySalt, 191) % 254u)));
            }
        }

        return damaged;
    }

    if (mode == 1)
    {
        int span = dataEnd - dataStart;
        int maxLength = Math.Max(1, span / 3);
        int length = ClampInt(8 + (int)(Random01(seed, 0, intensitySalt, 193) * 64.0), 1, maxLength);
        length = (int)(length * damageIntensity);
        length = Math.Max(1, length);
        int from = dataStart + (int)(Hash(seed, 0, intensitySalt, 197) % (uint)(span - length));
        int insertAt = dataStart + (int)(Hash(seed, 0, intensitySalt, 199) % (uint)span);
        byte[] damaged = new byte[source.Length + length];
        System.Array.Copy(source, 0, damaged, 0, insertAt);
        System.Array.Copy(source, from, damaged, insertAt, length);
        System.Array.Copy(source, insertAt, damaged, insertAt + length, source.Length - insertAt);
        return damaged;
    }

    if (mode == 2)
    {
        int removeSpan = dataEnd - dataStart;
        int maxRemoveLength = Math.Max(1, removeSpan / 4);
        int removeLength = ClampInt(8 + (int)(Random01(seed, 0, intensitySalt, 211) * 48.0), 1, maxRemoveLength);
        removeLength = (int)(removeLength * damageIntensity);
        removeLength = Math.Max(1, removeLength);
        int removeAt = dataStart + (int)(Hash(seed, 0, intensitySalt, 223) % (uint)(removeSpan - removeLength));
        byte[] removed = new byte[source.Length - removeLength];
        System.Array.Copy(source, 0, removed, 0, removeAt);
        System.Array.Copy(source, removeAt + removeLength, removed, removeAt, source.Length - removeAt - removeLength);
        return removed;
    }
    
    // Mode 3: Byte relocation/shift - move a section of bytes to a different location
    int moveSpan = dataEnd - dataStart;
    int maxMoveSize = Math.Max(32, moveSpan / 8);
    int moveSize = (int)(8 + (Random01(seed, 0, intensitySalt, 231) * (maxMoveSize - 8)));
    moveSize = (int)(moveSize * damageIntensity);
    moveSize = Math.Max(8, moveSize);
    moveSize = Math.Min(moveSize, moveSpan - 16);
    
    int sourceOffset = dataStart + (int)(Hash(seed, 0, intensitySalt, 233) % (uint)(moveSpan - moveSize));
    int targetOffset = dataStart + (int)(Hash(seed, 1, intensitySalt, 237) % (uint)(moveSpan - moveSize));
    
    byte[] movedDamaged = CopyBytes(source);
    byte[] temp = new byte[moveSize];
    
    // Save bytes from source location
    System.Array.Copy(movedDamaged, sourceOffset, temp, 0, moveSize);
    
    // Perform the relocation: shift data to make room and place moved bytes at target
    if (sourceOffset < targetOffset)
    {
        // Shift right: move bytes between source and target to the left
        System.Array.Copy(movedDamaged, sourceOffset + moveSize, movedDamaged, sourceOffset, targetOffset - sourceOffset);
        System.Array.Copy(temp, 0, movedDamaged, targetOffset, moveSize);
    }
    else
    {
        // Shift left: move bytes between target and source to the right
        System.Array.Copy(movedDamaged, targetOffset, movedDamaged, targetOffset + moveSize, sourceOffset - targetOffset);
        System.Array.Copy(temp, 0, movedDamaged, targetOffset, moveSize);
    }
    
    return movedDamaged;
}

int FindJpegEntropyStart(byte[] bytes)
{
    for (int i = 0; i < bytes.Length - 4; i++)
    {
        if (bytes[i] == 255 && bytes[i + 1] == 218)
        {
            int length = (bytes[i + 2] << 8) + bytes[i + 3];
            return i + 2 + length;
        }
    }

    return -1;
}

int FindJpegEntropyEnd(byte[] bytes)
{
    for (int i = bytes.Length - 2; i >= 0; i--)
    {
        if (bytes[i] == 255 && bytes[i + 1] == 217)
        {
            return i;
        }
    }

    return bytes.Length;
}

byte[] CopyBytes(byte[] source)
{
    byte[] copy = new byte[source.Length];
    System.Array.Copy(source, copy, source.Length);
    return copy;
}

System.Drawing.Imaging.ImageCodecInfo GetJpegCodec()
{
    System.Drawing.Imaging.ImageCodecInfo[] codecs = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders();
    for (int i = 0; i < codecs.Length; i++)
    {
        if (codecs[i].MimeType == "image/jpeg")
        {
            return codecs[i];
        }
    }

    return null;
}

System.Drawing.Imaging.ImageCodecInfo GetPngCodec()
{
    System.Drawing.Imaging.ImageCodecInfo[] codecs = System.Drawing.Imaging.ImageCodecInfo.GetImageEncoders();
    for (int i = 0; i < codecs.Length; i++)
    {
        if (codecs[i].MimeType == "image/png")
        {
            return codecs[i];
        }
    }

    return null;
}

byte[] EncodeSurfaceAsPngBytes(Surface src)
{
    using (System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(src.Width, src.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
    {
        for (int y = 0; y < src.Height; y++)
        {
            for (int x = 0; x < src.Width; x++)
            {
                ColorBgra c = src[x, y];
                bitmap.SetPixel(x, y, System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B));
            }
        }

        using (System.IO.MemoryStream stream = new System.IO.MemoryStream())
        {
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            return stream.ToArray();
        }
    }
}

Surface DecodePngBytesToSurface(byte[] pngBytes, int width, int height, Surface fallback)
{
    try
    {
        using (System.IO.MemoryStream stream = new System.IO.MemoryStream(pngBytes))
        {
            using (System.Drawing.Bitmap decoded = new System.Drawing.Bitmap(stream))
            {
                return CopyBitmapToSurface(decoded, width, height);
            }
        }
    }
    catch
    {
        return fallback;
    }
}

byte[] DamagePngBytes(byte[] source, int seed, double damageIntensity)
{
    // PNG structure: signature (8 bytes) + chunks
    // To keep PNG decodable, we target damage within IDAT chunks only (compressed image data)
    // This preserves critical headers and CRC checksums
    if (source.Length < 32)
    {
        return source;
    }

    // Find IDAT chunks (compressed image data)
    int[] idatRanges = FindPngIdatRanges(source);
    if (idatRanges.Length < 2)
    {
        // No IDAT chunks found, return undamaged
        return source;
    }

    int idatStart = idatRanges[0];
    int idatEnd = idatRanges[1];
    if (idatEnd <= idatStart + 8)
    {
        return source;
    }

    // Incorporate intensity into randomness so patterns change with slider
    int intensitySalt = (int)(damageIntensity * 1000.0);
    
    int mode = (int)(Hash(seed, 0, intensitySalt, 261) % 4u);
    
    if (mode == 0)
    {
        // Bit flip mode: flip bits within IDAT data only
        byte[] damaged = CopyBytes(source);
        int edits = (int)((6 + (int)(Random01(seed, 0, intensitySalt, 263) * 18.0)) * damageIntensity);
        int idatSpan = idatEnd - idatStart;
        
        for (int i = 0; i < edits; i++)
        {
            int index = idatStart + (int)(Hash(seed, i, intensitySalt, 267) % (uint)idatSpan);
            if (index < damaged.Length && damaged[index] != 255)
            {
                damaged[index] = (byte)(damaged[index] ^ (byte)(1 + (Hash(seed, i, intensitySalt, 271) % 254u)));
            }
        }

        return damaged;
    }

    if (mode == 1)
    {
        // Subtle channel noise: modify low bits in IDAT chunks
        byte[] damaged = CopyBytes(source);
        int noiseEdits = (int)((4 + (int)(Random01(seed, 0, intensitySalt, 273) * 12.0)) * damageIntensity);
        int idatSpan = idatEnd - idatStart;
        
        for (int i = 0; i < noiseEdits; i++)
        {
            int index = idatStart + (int)(Hash(seed, i, intensitySalt, 277) % (uint)idatSpan);
            if (index < damaged.Length)
            {
                // Modify only lower bits to preserve overall structure
                damaged[index] = (byte)((damaged[index] & 0xF0) | ((byte)Hash(seed, i, intensitySalt, 281) & 0x0F));
            }
        }

        return damaged;
    }

    if (mode == 2)
    {
        // Small-scale deletion within IDAT (more conservative)
        int idatSpan = idatEnd - idatStart;
        int maxRemoveLength = Math.Max(1, idatSpan / 12);  // Much smaller than JPEG
        int removeLength = ClampInt(2 + (int)(Random01(seed, 0, intensitySalt, 283) * 8.0), 1, maxRemoveLength);
        removeLength = (int)(removeLength * damageIntensity);
        removeLength = Math.Max(1, removeLength);
        
        if (removeLength >= idatSpan - 4)
            return source;  // Too much damage, skip
        
        int removeAt = idatStart + (int)(Hash(seed, 0, intensitySalt, 287) % (uint)(idatSpan - removeLength));
        
        byte[] removed = new byte[source.Length - removeLength];
        System.Array.Copy(source, 0, removed, 0, removeAt);
        System.Array.Copy(source, removeAt + removeLength, removed, removeAt, source.Length - removeAt - removeLength);
        return removed;
    }

    // Mode 3: Small byte swaps within IDAT (less destructive than relocation)
    int swapCount = (int)((2 + (int)(Random01(seed, 0, intensitySalt, 289) * 6.0)) * damageIntensity);
    byte[] swapped = CopyBytes(source);
    int idatSpan2 = idatEnd - idatStart;
    
    for (int i = 0; i < swapCount; i++)
    {
        int pos1 = idatStart + (int)(Hash(seed, i * 2, intensitySalt, 291) % (uint)idatSpan2);
        int pos2 = idatStart + (int)(Hash(seed, i * 2 + 1, intensitySalt, 293) % (uint)idatSpan2);
        
        if (pos1 < swapped.Length && pos2 < swapped.Length)
        {
            byte temp = swapped[pos1];
            swapped[pos1] = swapped[pos2];
            swapped[pos2] = temp;
        }
    }
    
    return swapped;
}

int[] FindPngIdatRanges(byte[] pngBytes)
{
    // PNG signature: 89 50 4E 47 0D 0A 1A 0A (8 bytes)
    // Then chunks: 4-byte length, 4-byte type, data, 4-byte CRC
    // IDAT chunk type: 49 44 41 54
    
    int firstIdatStart = -1;
    int lastIdatEnd = -1;
    
    int pos = 8;  // Skip signature
    while (pos < pngBytes.Length - 12)
    {
        if (pos + 8 > pngBytes.Length) break;
        
        int chunkLen = (pngBytes[pos] << 24) | (pngBytes[pos + 1] << 16) | 
                       (pngBytes[pos + 2] << 8) | pngBytes[pos + 3];
        
        // Check if type is IDAT (49 44 41 54)
        if (pngBytes[pos + 4] == 0x49 && pngBytes[pos + 5] == 0x44 &&
            pngBytes[pos + 6] == 0x41 && pngBytes[pos + 7] == 0x54)
        {
            if (firstIdatStart < 0)
            {
                firstIdatStart = pos + 8;  // Start of IDAT data
            }
            lastIdatEnd = pos + 8 + chunkLen;  // End of IDAT data
        }
        
        pos += 12 + chunkLen;  // Move to next chunk
    }
    
    if (firstIdatStart < 0 || lastIdatEnd < 0)
    {
        return new int[] { };  // No IDAT chunks found
    }
    
    return new int[] { firstIdatStart, lastIdatEnd };
}

Surface CreatePngBinaryDamageSurface(Surface src, int seed, double damageIntensity)
{
    byte[] pngBytes = EncodeSurfaceAsPngBytes(src);
    if (pngBytes == null)
    {
        return CopySourceToSurface(src);
    }

    byte[] damagedBytes = DamagePngBytes(pngBytes, seed, damageIntensity);
    Surface fallback = DecodePngBytesToSurface(pngBytes, src.Width, src.Height, CopySourceToSurface(src));
    return DecodePngBytesToSurface(damagedBytes, src.Width, src.Height, fallback);
}

byte[] EncodeSurfaceAsGifBytes(Surface src)
{
    using (System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(src.Width, src.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
    {
        for (int y = 0; y < src.Height; y++)
        {
            for (int x = 0; x < src.Width; x++)
            {
                ColorBgra c = src[x, y];
                bitmap.SetPixel(x, y, System.Drawing.Color.FromArgb(c.A, c.R, c.G, c.B));
            }
        }

        using (System.IO.MemoryStream stream = new System.IO.MemoryStream())
        {
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Gif);
            return stream.ToArray();
        }
    }
}

Surface DecodeGifBytesToSurface(byte[] gifBytes, int width, int height, Surface fallback)
{
    try
    {
        using (System.IO.MemoryStream stream = new System.IO.MemoryStream(gifBytes))
        {
            using (System.Drawing.Bitmap decoded = new System.Drawing.Bitmap(stream))
            {
                return CopyBitmapToSurface(decoded, width, height);
            }
        }
    }
    catch
    {
        return fallback;
    }
}

byte[] DamageGifBytes(byte[] source, int seed, double damageIntensity)
{
    // GIF structure: signature (3 bytes) + version (3) + logical screen descriptor (7) + global color table (optional)
    // To keep GIF decodable, we target damage in image data blocks only
    if (source.Length < 32)
    {
        return source;
    }

    // Find image data blocks (GIF supports multiple frames, we target all image data)
    int[] imageDataRanges = FindGifImageDataRanges(source);
    if (imageDataRanges.Length < 2)
    {
        // No image data found, return undamaged
        return source;
    }

    int dataStart = imageDataRanges[0];
    int dataEnd = imageDataRanges[1];
    if (dataEnd <= dataStart + 8)
    {
        return source;
    }

    // Incorporate intensity into randomness so patterns change with slider
    int intensitySalt = (int)(damageIntensity * 1000.0);
    
    int mode = (int)(Hash(seed, 0, intensitySalt, 301) % 4u);
    
    if (mode == 0)
    {
        // Bit flip mode: flip bits within image data only
        byte[] damaged = CopyBytes(source);
        int edits = (int)((5 + (int)(Random01(seed, 0, intensitySalt, 303) * 15.0)) * damageIntensity);
        int dataSpan = dataEnd - dataStart;
        
        for (int i = 0; i < edits; i++)
        {
            int index = dataStart + (int)(Hash(seed, i, intensitySalt, 307) % (uint)dataSpan);
            if (index < damaged.Length && damaged[index] != 255)
            {
                damaged[index] = (byte)(damaged[index] ^ (byte)(1 + (Hash(seed, i, intensitySalt, 311) % 254u)));
            }
        }

        return damaged;
    }

    if (mode == 1)
    {
        // Subtle noise: modify low bits in image data
        byte[] damaged = CopyBytes(source);
        int noiseEdits = (int)((3 + (int)(Random01(seed, 0, intensitySalt, 313) * 9.0)) * damageIntensity);
        int dataSpan = dataEnd - dataStart;
        
        for (int i = 0; i < noiseEdits; i++)
        {
            int index = dataStart + (int)(Hash(seed, i, intensitySalt, 317) % (uint)dataSpan);
            if (index < damaged.Length)
            {
                // Modify only lower bits to preserve overall structure
                damaged[index] = (byte)((damaged[index] & 0xF0) | ((byte)Hash(seed, i, intensitySalt, 321) & 0x0F));
            }
        }

        return damaged;
    }

    if (mode == 2)
    {
        // Small-scale deletion within image data (conservative)
        int dataSpan = dataEnd - dataStart;
        int maxRemoveLength = Math.Max(1, dataSpan / 10);  // Conservative for GIF
        int removeLength = ClampInt(2 + (int)(Random01(seed, 0, intensitySalt, 323) * 6.0), 1, maxRemoveLength);
        removeLength = (int)(removeLength * damageIntensity);
        removeLength = Math.Max(1, removeLength);
        
        if (removeLength >= dataSpan - 4)
            return source;  // Too much damage, skip
        
        int removeAt = dataStart + (int)(Hash(seed, 0, intensitySalt, 327) % (uint)(dataSpan - removeLength));
        
        byte[] removed = new byte[source.Length - removeLength];
        System.Array.Copy(source, 0, removed, 0, removeAt);
        System.Array.Copy(source, removeAt + removeLength, removed, removeAt, source.Length - removeAt - removeLength);
        return removed;
    }

    // Mode 3: Small byte swaps within image data
    int swapCount = (int)((2 + (int)(Random01(seed, 0, intensitySalt, 329) * 5.0)) * damageIntensity);
    byte[] swapped = CopyBytes(source);
    int dataSpan2 = dataEnd - dataStart;
    
    for (int i = 0; i < swapCount; i++)
    {
        int pos1 = dataStart + (int)(Hash(seed, i * 2, intensitySalt, 331) % (uint)dataSpan2);
        int pos2 = dataStart + (int)(Hash(seed, i * 2 + 1, intensitySalt, 333) % (uint)dataSpan2);
        
        if (pos1 < swapped.Length && pos2 < swapped.Length)
        {
            byte temp = swapped[pos1];
            swapped[pos1] = swapped[pos2];
            swapped[pos2] = temp;
        }
    }
    
    return swapped;
}

int[] FindGifImageDataRanges(byte[] gifBytes)
{
    // GIF signature: 47 49 46 (3 bytes "GIF")
    // Version: 38 39 61 or 38 37 61 (3 bytes "89a" or "87a")
    // Logical Screen Descriptor: 7 bytes
    // Global Color Table: optional (variable size)
    // Image Descriptor: 0x2C (1 byte)
    // Local Color Table: optional
    // LZW minimum code size: 1 byte
    // Image data: variable-length blocks
    // Block terminator: 0x00
    
    int firstDataStart = -1;
    int lastDataEnd = -1;
    
    // Scan for image descriptors (0x2C) and associated data blocks
    int pos = 6;  // Skip "GIF" signature and version
    
    if (pos + 7 > gifBytes.Length) return new int[] { };
    
    int lsdWidth = gifBytes[pos] | (gifBytes[pos + 1] << 8);
    int lsdHeight = gifBytes[pos + 2] | (gifBytes[pos + 3] << 8);
    int lsdFlags = gifBytes[pos + 4];
    pos += 7;
    
    // Skip Global Color Table if present
    if ((lsdFlags & 0x80) != 0)
    {
        int gctSize = 2 << (lsdFlags & 0x07);
        pos += gctSize * 3;
    }
    
    // Scan for image data
    while (pos < gifBytes.Length)
    {
        byte separator = gifBytes[pos];
        
        if (separator == 0x2C)  // Image Descriptor
        {
            pos++;  // Skip separator
            if (pos + 8 > gifBytes.Length) break;
            
            pos += 8;  // Skip image position and size
            int imageFlags = gifBytes[pos];
            pos++;
            
            // Skip Local Color Table if present
            if ((imageFlags & 0x80) != 0)
            {
                int lctSize = 2 << (imageFlags & 0x07);
                pos += lctSize * 3;
            }
            
            // Now we're at LZW minimum code size
            if (pos >= gifBytes.Length) break;
            pos++;  // Skip LZW min code size
            
            // Record start of image data blocks
            if (firstDataStart < 0)
                firstDataStart = pos;
            
            // Skip image data blocks (each block starts with size byte, 0x00 terminates)
            while (pos < gifBytes.Length && gifBytes[pos] != 0)
            {
                int blockSize = gifBytes[pos];
                pos += 1 + blockSize;
            }
            
            if (pos < gifBytes.Length && gifBytes[pos] == 0)
            {
                lastDataEnd = pos;  // Include the block terminator
                pos++;
            }
        }
        else if (separator == 0x21)  // Extension
        {
            pos++;
            if (pos >= gifBytes.Length) break;
            
            byte label = gifBytes[pos];
            pos++;
            
            // Skip extension data blocks
            while (pos < gifBytes.Length && gifBytes[pos] != 0)
            {
                int blockSize = gifBytes[pos];
                pos += 1 + blockSize;
            }
            
            if (pos < gifBytes.Length && gifBytes[pos] == 0)
                pos++;  // Skip block terminator
        }
        else if (separator == 0x3B)  // Trailer
        {
            break;
        }
        else
        {
            pos++;
        }
    }
    
    if (firstDataStart < 0 || lastDataEnd < 0)
    {
        return new int[] { };  // No image data found
    }
    
    return new int[] { firstDataStart, lastDataEnd };
}

Surface CreateGifBinaryDamageSurface(Surface src, int seed, double damageIntensity)
{
    byte[] gifBytes = EncodeSurfaceAsGifBytes(src);
    if (gifBytes == null)
    {
        return CopySourceToSurface(src);
    }

    byte[] damagedBytes = DamageGifBytes(gifBytes, seed, damageIntensity);
    Surface fallback = DecodeGifBytesToSurface(gifBytes, src.Width, src.Height, CopySourceToSurface(src));
    return DecodeGifBytesToSurface(damagedBytes, src.Width, src.Height, fallback);
}

Surface CopyBitmapToSurface(System.Drawing.Bitmap bitmap, int width, int height)
{
    Surface surface = new Surface(width, height);
    for (int y = 0; y < height; y++)
    {
        for (int x = 0; x < width; x++)
        {
            System.Drawing.Color c = bitmap.GetPixel(x, y);
            surface[x, y] = ColorBgra.FromBgra(c.B, c.G, c.R, 255);
        }
    }

    return surface;
}

Surface CopySourceToSurface(Surface src)
{
    Surface surface = new Surface(src.Width, src.Height);
    for (int y = 0; y < src.Height; y++)
    {
        for (int x = 0; x < src.Width; x++)
        {
            surface[x, y] = src[x, y];
        }
    }

    return surface;
}

ColorBgra MissingPacketRows(Surface src, int x, int y, int seed, int blockSize, double intensity)
{
    int stripHeight = Math.Max(2, blockSize);
    int strip = y / stripHeight;
    uint h = Hash(seed, strip, 0, 23);
    int trigger = (int)(h % 100u);
    int lossThreshold = 20 + (int)(35.0 * intensity);
    int bandVariance = Math.Max(1, stripHeight + (int)(stripHeight * intensity));
    int bandHeight = ClampInt(stripHeight + (int)(Hash(seed, strip, 0, 35) % (uint)(bandVariance + 1)), 2, stripHeight + bandVariance);

    if (trigger > lossThreshold)
    {
        return src[x, y];
    }

    // Packet loss reads better as dropped or repeated scanline bands than as
    // arbitrary pixel corruption.
    int mode = (int)(Hash(seed, strip, 0, 29) % 5u);
    int baseRow = (y / bandHeight) * bandHeight;
    int localRow = y - baseRow;
    int bandRow = baseRow + localRow;

    if (mode == 0)
    {
        // Drop the band entirely.
        return ColorBgra.FromBgra(0, 0, 0, 0);
    }

    if (mode == 1)
    {
        // Duplicate the previous band.
        int sourceY = ClampInt(baseRow - bandHeight + localRow, 0, src.Height - 1);
        return SampleClamped(src, x, sourceY);
    }

    if (mode == 2)
    {
        // Duplicate the next band.
        int sourceY = ClampInt(baseRow + bandHeight + localRow, 0, src.Height - 1);
        return SampleClamped(src, x, sourceY);
    }

    if (mode == 3)
    {
        // Skew the band by a row-dependent horizontal stride.
        int skew = HashSigned(seed, strip, 0, 31) % Math.Max(1, bandHeight * 2);
        int sourceX = ClampInt(x + skew, 0, src.Width - 1);
        int sourceY = ClampInt(bandRow + ((h & 1u) == 0u ? -1 : 1) * bandHeight, 0, src.Height - 1);
        return SampleClamped(src, sourceX, sourceY);
    }

    // Partial packet recovery: mix a repeated line with a small adjacent offset.
    int offsetRows = ((h & 1u) == 0u ? -1 : 1) * (bandHeight + (int)(h % (uint)(bandHeight * 2)));
    int shift = HashSigned(seed, strip, 0, 33) % Math.Max(1, blockSize * 2 + bandHeight / 2);
    int sx = ClampInt(x + shift, 0, src.Width - 1);
    int sy = ClampInt(y + offsetRows, 0, src.Height - 1);
    ColorBgra c = SampleClamped(src, sx, sy);

    if ((h & 4u) != 0u)
    {
        c = ColorBgra.FromBgra(
            (byte)ClampByte(c.B - (int)(28.0 * intensity)),
            (byte)ClampByte(c.G - (int)(20.0 * intensity)),
            (byte)ClampByte(c.R - (int)(8.0 * intensity)),
            c.A);
    }

    return c;
}

ColorBgra ChannelSwapLab(ColorBgra c, int seed, int x, int y)
{
    int variant = (int)(Hash(seed, 0, 0, 37) % 12u);

    switch (variant)
    {
        case 0:
            return ColorBgra.FromBgra(c.B, c.R, c.G, c.A);
        case 1:
            return ColorBgra.FromBgra(c.G, c.B, c.R, c.A);
        case 2:
            return ColorBgra.FromBgra(c.R, c.G, c.B, c.A);
        case 3:
            return ColorBgra.FromBgra(c.G, c.R, c.B, c.A);
        case 4:
            return SwapFakeHsk(c, 0);
        case 5:
            return SwapFakeHsk(c, 1);
        case 6:
            return SwapFakeHsk(c, 2);
        case 7:
            return CrossWireRgbHsk(c, 0);
        case 8:
            return CrossWireRgbHsk(c, 1);
        case 9:
            return CrossWireRgbHsk(c, 2);
        case 10:
            return CrossWireRgbHsk(c, 3);
        default:
            return ColorBgra.FromBgra(c.R, c.B, c.G, c.A);
    }
}

ColorBgra SwapFakeHsk(ColorBgra c, int variant)
{
    double h;
    double s;
    double k;
    RgbToFakeHsk(c, out h, out s, out k);

    if (variant == 0)
    {
        double temp = h;
        h = s;
        s = temp;
    }
    else if (variant == 1)
    {
        double temp = h;
        h = k;
        k = temp;
    }
    else
    {
        double temp = s;
        s = k;
        k = temp;
    }

    return FakeHskToRgb(h, s, k, c.A);
}

ColorBgra CrossWireRgbHsk(ColorBgra c, int variant)
{
    double h;
    double s;
    double k;
    RgbToFakeHsk(c, out h, out s, out k);

    int r = c.R;
    int g = c.G;
    int b = c.B;
    int hh = ClampByte((int)(h * 255.0));
    int ss = ClampByte((int)(s * 255.0));
    int kk = ClampByte((int)(k * 255.0));

    if (variant == 0)
    {
        r = ss;
        g = kk;
        b = hh;
    }
    else if (variant == 1)
    {
        r = kk;
        g = c.B;
        b = hh;
    }
    else if (variant == 2)
    {
        r = c.G;
        g = hh;
        b = ss;
    }
    else
    {
        r = WrapByte(c.R + kk);
        g = WrapByte(c.G + hh);
        b = WrapByte(c.B + ss);
    }

    return ColorBgra.FromBgra((byte)b, (byte)g, (byte)r, c.A);
}

ColorBgra LackOfClamping(ColorBgra c, int seed, int x, int y, double intensity)
{
    // Choose which channel to distort: 0-2 = R,G,B; 3-5 = H,S,K (same for entire image)
    int channel = (int)(Hash(seed, 0, 0, 43) % 6u);
    
    // Derive offset and multiplier from seed (same for entire image)
    int offsetRaw = HashSigned(seed, 0, 0, 47);
    int multiplierRaw = (int)(Hash(seed, 0, 0, 53) % 200u);
    
    // Scale by intensity (increased for stronger visual effect)
    int offset = (int)(offsetRaw * intensity * 5.5);
    double multiplier = 0.05 + ((multiplierRaw / 200.0) * intensity * 3.0);  // Range: 0.05 to 3.05
    
    if (channel < 3)
    {
        // Affect RGB channel
        int val = (channel == 0) ? c.R : (channel == 1) ? c.G : c.B;
        int newVal = (int)((val * multiplier + offset) % 256);
        if (newVal < 0) newVal = (newVal % 256 + 256) % 256;
        
        if (channel == 0) return ColorBgra.FromBgra(c.B, c.G, (byte)newVal, c.A);
        if (channel == 1) return ColorBgra.FromBgra(c.B, (byte)newVal, c.R, c.A);
        return ColorBgra.FromBgra((byte)newVal, c.G, c.R, c.A);
    }
    else
    {
        // Affect HSK channel
        double h, s, k;
        RgbToFakeHsk(c, out h, out s, out k);
        
        int hskIdx = channel - 3;
        double val = (hskIdx == 0) ? h : (hskIdx == 1) ? s : k;
        
        // Apply multiplier and offset, then modulo to [0, 1]
        double newVal = val * multiplier + (offset / 1000.0);
        newVal = newVal - Math.Floor(newVal);  // Fractional modulo
        if (newVal < 0.0) newVal += 1.0;
        
        if (hskIdx == 0) h = newVal;
        else if (hskIdx == 1) s = newVal;
        else k = newVal;
        
        return FakeHskToRgb(h, s, k, c.A);
    }
}

ColorBgra MemoryTear(Surface src, int x, int y, int seed, double intensity)
{
    return MemoryTear(src, x, y, seed, intensity, 0);
}

ColorBgra MemoryTear(Surface src, int x, int y, int seed, double intensity, int volatileSalt)
{
    // Simulate a concurrent writer sweeping the frame while reader samples torn data.
    int frameSalt = seed ^ (volatileSalt * 1315423911);

    bool horizontalSweep = (Hash(frameSalt, 0, 0, 401) & 1u) == 0u;
    int span = horizontalSweep ? src.Width : src.Height;
    int writeHead = (int)(Hash(frameSalt, 0, 0, 403) % (uint)Math.Max(1, span));
    int readPos = horizontalSweep ? x : y;

    int tearWidth = Math.Max(3, (int)(3 + (17.0 * intensity)));
    int dist = readPos - writeHead;
    if (dist < 0)
    {
        dist += span;
    }

    ColorBgra stable = src[x, y];
    if (dist > tearWidth)
    {
        return stable;
    }

    int cacheLinePixels = 8; // 32-byte cache line at 4 bytes/pixel.
    int lineCoord = horizontalSweep ? x : y;
    int linePhase = lineCoord & (cacheLinePixels - 1);
    int partialCutoff = (int)(Hash(frameSalt, x / cacheLinePixels, y / cacheLinePixels, 409) % (uint)cacheLinePixels);

    int tearShift = 2 + (int)(intensity * 10.0);
    int wobble = HashSigned(frameSalt, x / 8, y / 8, 411) % Math.Max(1, tearShift);

    int oldX = horizontalSweep ? x - tearShift + wobble : x + ((linePhase < partialCutoff) ? -1 : 1);
    int oldY = horizontalSweep ? y + ((linePhase < partialCutoff) ? -1 : 1) : y - tearShift + wobble;
    int newX = horizontalSweep ? x + tearShift + wobble : x + ((linePhase < partialCutoff) ? 1 : -1);
    int newY = horizontalSweep ? y + ((linePhase < partialCutoff) ? 1 : -1) : y + tearShift + wobble;

    ColorBgra oldSample = SampleClamped(src, oldX, oldY);
    ColorBgra newSample = SampleClamped(src, newX, newY);

    bool blueNew = linePhase >= partialCutoff;
    bool greenNew = ((linePhase + 2) & 7) >= partialCutoff;
    bool redNew = ((linePhase + 4) & 7) >= partialCutoff;

    int severity = ClampByte((int)(100.0 + (intensity * 145.0)));
    byte b = (byte)((blueNew ? newSample.B : oldSample.B) * severity / 255 + stable.B * (255 - severity) / 255);
    byte g = (byte)((greenNew ? newSample.G : oldSample.G) * severity / 255 + stable.G * (255 - severity) / 255);
    byte r = (byte)((redNew ? newSample.R : oldSample.R) * severity / 255 + stable.R * (255 - severity) / 255);

    return ColorBgra.FromBgra(b, g, r, stable.A);
}

ColorBgra MismappedChannel(Surface src, ColorBgra current, int x, int y, int seed)
{
    int channel = (int)(Hash(seed, 0, 0, 151) % 6u);
    int transform = (int)(Hash(seed, 0, 0, 157) % 3u);
    int sx;
    int sy;
    GetTransformedCoordinate(src, x, y, seed, transform, out sx, out sy);

    ColorBgra sampled = SampleClamped(src, sx, sy);

    if (channel < 3)
    {
        return ReplaceRgbChannel(current, sampled, channel);
    }

    double h;
    double s;
    double k;
    double sampleH;
    double sampleS;
    double sampleK;
    RgbToFakeHsk(current, out h, out s, out k);
    RgbToFakeHsk(sampled, out sampleH, out sampleS, out sampleK);

    if (channel == 3) h = sampleH;
    else if (channel == 4) s = sampleS;
    else k = sampleK;

    return FakeHskToRgb(h, s, k, current.A);
}

void GetTransformedCoordinate(Surface src, int x, int y, int seed, int transform, out int sx, out int sy)
{
    double cx = (src.Width - 1) * 0.5;
    double cy = (src.Height - 1) * 0.5;
    double dx = x - cx;
    double dy = y - cy;

    if (transform == 0)
    {
        double stretch = 0.65 + (Random01(seed, 0, 0, 163) * 0.85);
        sx = ClampInt((int)Math.Floor(cx + (dx * stretch) + 0.5), 0, src.Width - 1);
        sy = ClampInt((int)Math.Floor(cy + dy + 0.5), 0, src.Height - 1);
        return;
    }

    if (transform == 1)
    {
        double scale = 0.70 + (Random01(seed, 0, 0, 167) * 0.75);
        sx = ClampInt((int)Math.Floor(cx + (dx / scale) + 0.5), 0, src.Width - 1);
        sy = ClampInt((int)Math.Floor(cy + (dy / scale) + 0.5), 0, src.Height - 1);
        return;
    }

    double angle = (Random01(seed, 0, 0, 173) - 0.5) * 0.70;
    double ca = Math.Cos(angle);
    double sa = Math.Sin(angle);
    sx = ClampInt((int)Math.Floor(cx + (dx * ca) - (dy * sa) + 0.5), 0, src.Width - 1);
    sy = ClampInt((int)Math.Floor(cy + (dx * sa) + (dy * ca) + 0.5), 0, src.Height - 1);
}

ColorBgra ReplaceRgbChannel(ColorBgra current, ColorBgra sampled, int channel)
{
    byte r = current.R;
    byte g = current.G;
    byte b = current.B;

    if (channel == 0) r = sampled.R;
    else if (channel == 1) g = sampled.G;
    else b = sampled.B;

    return ColorBgra.FromBgra(b, g, r, current.A);
}

void RgbToFakeHsk(ColorBgra c, out double h, out double s, out double k)
{
    double r = c.R / 255.0;
    double g = c.G / 255.0;
    double b = c.B / 255.0;
    double max = Math.Max(r, Math.Max(g, b));
    double min = Math.Min(r, Math.Min(g, b));
    double span = max - min;

    h = (r - g + b + 1.0) * 0.5;
    s = span;
    k = 1.0 - ((r * 0.299) + (g * 0.587) + (b * 0.114));
}

ColorBgra FakeHskToRgb(double h, double s, double k, byte alpha)
{
    double v = 1.0 - k;
    double r = v + ((h - 0.5) * s);
    double g = v - ((h - 0.5) * s);
    double b = v + ((0.5 - Math.Abs(h - 0.5)) * s);

    return ColorBgra.FromBgra(
        (byte)ClampByte((int)(b * 255.0)),
        (byte)ClampByte((int)(g * 255.0)),
        (byte)ClampByte((int)(r * 255.0)),
        alpha);
}

ColorBgra BlendPreserveAlpha(ColorBgra a, ColorBgra b, double amount)
{
    if (amount <= 0.0) return a;
    if (amount >= 1.0) return ColorBgra.FromBgra(b.B, b.G, b.R, a.A);

    int blue = LerpByte(a.B, b.B, amount);
    int green = LerpByte(a.G, b.G, amount);
    int red = LerpByte(a.R, b.R, amount);
    return ColorBgra.FromBgra((byte)blue, (byte)green, (byte)red, a.A);
}

int LerpByte(byte a, byte b, double amount)
{
    return ClampByte((int)(a + ((b - a) * amount) + 0.5));
}

int Quantize(int value, int step)
{
    if (step <= 1) return ClampByte(value);
    return ClampByte((value / step) * step);
}

int ClampByte(int value)
{
    if (value < 0) return 0;
    if (value > 255) return 255;
    return value;
}

int WrapByte(int value)
{
    return value & 255;
}

int ClampInt(int value, int min, int max)
{
    if (value < min) return min;
    if (value > max) return max;
    return value;
}

ColorBgra SampleClamped(Surface src, int x, int y)
{
    return src[
        ClampInt(x, 0, src.Width - 1),
        ClampInt(y, 0, src.Height - 1)];
}

double Random01(int seed, int x, int y, int salt)
{
    return (Hash(seed, x, y, salt) & 0x00FFFFFFu) / 16777215.0;
}

int HashSigned(int seed, int x, int y, int salt)
{
    return (int)Hash(seed, x, y, salt);
}

int ComputeSettingsKey(
    int seed,
    bool mismappedChannelEnabled,
    bool compressionArtifactsEnabled,
    bool packetLossEnabled,
    bool dataTypeConfusionEnabled,
    bool lackOfClampingEnabled,
    bool memoryTearEnabled,
    bool jpegBinaryCorruptionEnabled,
    bool pngBinaryCorruptionEnabled,
    bool gifBinaryCorruptionEnabled,
    bool floatingPointErrorEnabled,
    int rotationMode,
    int volatileFrameSalt)
{
    unchecked
    {
        int key = seed;
        key = (key * 397) ^ (mismappedChannelEnabled ? 1 : 0);
        key = (key * 397) ^ (compressionArtifactsEnabled ? 1 : 0);
        key = (key * 397) ^ (packetLossEnabled ? 1 : 0);
        key = (key * 397) ^ (dataTypeConfusionEnabled ? 1 : 0);
        key = (key * 397) ^ (lackOfClampingEnabled ? 1 : 0);
        key = (key * 397) ^ (memoryTearEnabled ? 1 : 0);
        key = (key * 397) ^ (jpegBinaryCorruptionEnabled ? 1 : 0);
        key = (key * 397) ^ (pngBinaryCorruptionEnabled ? 1 : 0);
        key = (key * 397) ^ (gifBinaryCorruptionEnabled ? 1 : 0);
        key = (key * 397) ^ (floatingPointErrorEnabled ? 1 : 0);
        key = (key * 397) ^ rotationMode;
        key = (key * 397) ^ volatileFrameSalt;
        return key;
    }
}

Surface ApplyPreRotation(Surface src, int mode)
{
    int rotation = mode / 2;
    bool hFlip = (mode & 1) != 0;
    
    Surface result;
    
    if (rotation == 0)
    {
        result = new Surface(src.Width, src.Height);
    }
    else if (rotation == 1 || rotation == 3)
    {
        result = new Surface(src.Height, src.Width);
    }
    else
    {
        result = new Surface(src.Width, src.Height);
    }
    
    for (int y = 0; y < src.Height; y++)
    {
        for (int x = 0; x < src.Width; x++)
        {
            int tx = x, ty = y;
            
            if (rotation == 1)
            {
                tx = y;
                ty = src.Width - 1 - x;
            }
            else if (rotation == 2)
            {
                tx = src.Width - 1 - x;
                ty = src.Height - 1 - y;
            }
            else if (rotation == 3)
            {
                tx = src.Height - 1 - y;
                ty = x;
            }
            
            if (hFlip)
            {
                if (rotation == 0 || rotation == 2)
                {
                    tx = result.Width - 1 - tx;
                }
                else
                {
                    ty = result.Height - 1 - ty;
                }
            }
            
            if (tx >= 0 && tx < result.Width && ty >= 0 && ty < result.Height)
            {
                result[tx, ty] = src[x, y];
            }
        }
    }
    
    return result;
}

void ReversePreRotationCoords(int rotX, int rotY, int origW, int origH, int rotW, int rotH, int mode, out int origX, out int origY)
{
    int rotation = mode / 2;
    bool hFlip = (mode & 1) != 0;
    
    int x = rotX, y = rotY;
    
    if (hFlip)
    {
        if (rotation == 0 || rotation == 2)
        {
            x = rotW - 1 - x;
        }
        else
        {
            y = rotH - 1 - y;
        }
    }
    
    if (rotation == 1)
    {
        int temp = x;
        x = origW - 1 - y;
        y = temp;
    }
    else if (rotation == 2)
    {
        x = origW - 1 - x;
        y = origH - 1 - y;
    }
    else if (rotation == 3)
    {
        int temp = x;
        x = y;
        y = origH - 1 - temp;
    }
    
    origX = x;
    origY = y;
}

uint Hash(int seed, int x, int y, int salt)
{
    unchecked
    {
        uint h = (uint)seed;
        h ^= (uint)(x * 374761393);
        h = (h << 13) | (h >> 19);
        h ^= (uint)(y * 668265263);
        h *= 2246822519u;
        h ^= (uint)salt * 3266489917u;
        h ^= h >> 16;
        h *= 2246822519u;
        h ^= h >> 13;
        h *= 3266489917u;
        h ^= h >> 16;
        return h;
    }
}
