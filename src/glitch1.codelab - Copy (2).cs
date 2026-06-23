// glitch1 - Paint.NET CodeLab ClassicEffect script
//
// This script is structured as a coding exemplar: deterministic coordinate
// hashing, source-only sampling, alpha preservation, and minimal per-pixel work.
// In CodeLab, use/accept the ClassicEffect project type for this Surface API.

#region UICode
IntSliderControl Amount1 = 501; // [1,10000] Seed number
CheckboxControl Amount2 = true; // Compression Artifacts
CheckboxControl Amount3 = true; // Packet Loss
CheckboxControl Amount4 = true; // Data Type Confusion
CheckboxControl Amount5 = true; // Lack Of Clamping
CheckboxControl Amount6 = true; // Memory Alignment Tear
CheckboxControl Amount7 = true; // Single Channel Transform
IntSliderControl Amount8 = 0; // [0,200] JPEG Binary Damage intensity
IntSliderControl Amount9 = 0; // [0,200] PNG Binary Damage intensity
IntSliderControl Amount10 = 0; // [0,200] GIF Binary Damage intensity
#endregion

void Render(Surface dst, Surface src, Rectangle rect)
{
    int seed = Amount1;
    bool compressionArtifactsEnabled = Amount2;
    bool packetLossEnabled = Amount3;
    bool dataTypeConfusionEnabled = Amount4;
    bool lackOfClampingEnabled = Amount5;
    bool memoryAlignmentTearEnabled = Amount6;
    bool singleChannelTransformEnabled = Amount7;
    bool jpegBinaryDamageEnabled = Amount8 > 0;
    bool pngBinaryDamageEnabled = Amount9 > 0;
    bool gifBinaryDamageEnabled = Amount10 > 0;
    
    int rotationMode = 0;
    Surface rotatedSrc = src;
    int rotationModeValue = (int)(Hash(seed, 0, 0, 241) % 8u);
    rotationMode = rotationModeValue;
    rotatedSrc = ApplyPreRotation(src, rotationMode);
    if (!compressionArtifactsEnabled &&
        !packetLossEnabled &&
        !dataTypeConfusionEnabled &&
        !lackOfClampingEnabled &&
        !memoryAlignmentTearEnabled &&
        !singleChannelTransformEnabled &&
        !jpegBinaryDamageEnabled &&
        !pngBinaryDamageEnabled &&
        !gifBinaryDamageEnabled)
    {
        compressionArtifactsEnabled = Amount2;
        packetLossEnabled = !compressionArtifactsEnabled && Amount3;
        dataTypeConfusionEnabled = !compressionArtifactsEnabled && !packetLossEnabled && Amount4;
        lackOfClampingEnabled = !compressionArtifactsEnabled && !packetLossEnabled && !dataTypeConfusionEnabled && Amount5;
        memoryAlignmentTearEnabled = !compressionArtifactsEnabled && !packetLossEnabled && !dataTypeConfusionEnabled && !lackOfClampingEnabled && Amount6;
        singleChannelTransformEnabled = !compressionArtifactsEnabled && !packetLossEnabled && !dataTypeConfusionEnabled && !lackOfClampingEnabled && !memoryAlignmentTearEnabled && Amount7;
        jpegBinaryDamageEnabled = !compressionArtifactsEnabled && !packetLossEnabled && !dataTypeConfusionEnabled && !lackOfClampingEnabled && !memoryAlignmentTearEnabled && !singleChannelTransformEnabled && Amount8 > 0;
        pngBinaryDamageEnabled = !compressionArtifactsEnabled && !packetLossEnabled && !dataTypeConfusionEnabled && !lackOfClampingEnabled && !memoryAlignmentTearEnabled && !singleChannelTransformEnabled && Amount9 > 0;
        gifBinaryDamageEnabled = !compressionArtifactsEnabled && !packetLossEnabled && !dataTypeConfusionEnabled && !lackOfClampingEnabled && !memoryAlignmentTearEnabled && !singleChannelTransformEnabled && Amount10 > 0;
    }
    bool stackEffects = true;  // Always stack effects for consistent composition
    double intensity = 0.45 + (Random01(seed, 0, 0, 101) * 0.45);
    int blockSize = 4 + ((int)(Hash(seed, 0, 0, 103) % 8u) * 2);
    int jpegQuality = GetJpegQuality(seed, intensity);
    Surface jpegSurface = null;
    Surface damagedJpegSurface = null;
    PrepareJpegSurfaces(rotatedSrc, jpegQuality, seed, compressionArtifactsEnabled, out jpegSurface);
    int[] effectOrder = GetEffectOrder(
        seed,
        stackEffects,
        compressionArtifactsEnabled,
        packetLossEnabled,
        dataTypeConfusionEnabled,
        lackOfClampingEnabled,
        memoryAlignmentTearEnabled,
        singleChannelTransformEnabled,
        jpegBinaryDamageEnabled,
        pngBinaryDamageEnabled,
        gifBinaryDamageEnabled);
    if (jpegBinaryDamageEnabled)
    {
        double damageIntensity = Amount8 / 100.0;
        damagedJpegSurface = CreateJpegBinaryDamageSurface(rotatedSrc, 10, seed, damageIntensity);
    }

    Surface damagedPngSurface = null;
    if (pngBinaryDamageEnabled)
    {
        double damageIntensity = Amount9 / 100.0;
        
        // Determine which damage effect comes first in the shuffled order
        int jpegIndex = -1, pngIndex = -1, gifIndex = -1;
        for (int i = 0; i < effectOrder.Length; i++)
        {
            if (effectOrder[i] == 6) jpegIndex = i;
            if (effectOrder[i] == 7) pngIndex = i;
            if (effectOrder[i] == 8) gifIndex = i;
        }
        
        // If JPEG comes before PNG, apply PNG damage to the JPEG-damaged surface for chaining
        if (jpegIndex >= 0 && jpegIndex < pngIndex)
        {
            damagedPngSurface = CreatePngBinaryDamageSurface(damagedJpegSurface, seed, damageIntensity);
        }
        else
        {
            // Otherwise, apply PNG damage to the original source
            damagedPngSurface = CreatePngBinaryDamageSurface(rotatedSrc, seed, damageIntensity);
        }
    }

    Surface damagedGifSurface = null;
    if (gifBinaryDamageEnabled)
    {
        double damageIntensity = Amount10 / 100.0;
        
        // Determine which damage effect comes first in the shuffled order
        int jpegIndex = -1, pngIndex = -1, gifIndex = -1;
        for (int i = 0; i < effectOrder.Length; i++)
        {
            if (effectOrder[i] == 6) jpegIndex = i;
            if (effectOrder[i] == 7) pngIndex = i;
            if (effectOrder[i] == 8) gifIndex = i;
        }
        
        // Chain based on which damage comes first before GIF
        Surface gifSource = rotatedSrc;
        
        if (jpegIndex >= 0 && jpegIndex < gifIndex)
        {
            gifSource = damagedJpegSurface;
        }
        else if (pngIndex >= 0 && pngIndex < gifIndex)
        {
            gifSource = damagedPngSurface;
        }
        
        damagedGifSurface = CreateGifBinaryDamageSurface(gifSource, seed, damageIntensity);
    }

    // Create accumulated surface for effects that sample neighbors (Memory Alignment Tear, Single Channel Transform)
    Surface accumulatedResult = new Surface(rotatedSrc.Width, rotatedSrc.Height);
    
    for (int y = 0; y < rotatedSrc.Height; y++)
    {
        for (int x = 0; x < rotatedSrc.Width; x++)
        {
            ColorBgra original = rotatedSrc[x, y];
            ColorBgra result = original;
            
            // Store original in accumulated surface
            accumulatedResult[x, y] = result;
            
            double amount = intensity;

            // Apply all effects in shuffled order (JPEG and PNG damage will chain together)
            for (int i = 0; i < effectOrder.Length; i++)
            {
                int effectId = effectOrder[i];
                    
                result = ApplyEffectById(
                    effectId,
                    result,
                    rotatedSrc,
                    jpegSurface,
                    damagedJpegSurface,
                    damagedPngSurface,
                    damagedGifSurface,
                    accumulatedResult,
                    x,
                    y,
                    seed,
                    blockSize,
                    intensity,
                    amount,
                    compressionArtifactsEnabled,
                    packetLossEnabled,
                    dataTypeConfusionEnabled,
                    lackOfClampingEnabled,
                    memoryAlignmentTearEnabled,
                    singleChannelTransformEnabled,
                    jpegBinaryDamageEnabled,
                    pngBinaryDamageEnabled,
                    gifBinaryDamageEnabled);
            }

            // Update accumulated surface with final result
            accumulatedResult[x, y] = result;

            int dstX, dstY;
            ReversePreRotationCoords(x, y, src.Width, src.Height, rotatedSrc.Width, rotatedSrc.Height, rotationMode, out dstX, out dstY);
            if (dstX >= 0 && dstX < src.Width && dstY >= 0 && dstY < src.Height)
            {
                dst[dstX, dstY] = result;
            }
        }
    }
}



Surface RenderStackToSurface(
    Surface src,
    Surface jpegSurface,
    int[] effectOrder,
    int seed,
    int blockSize,
    double intensity,
    bool compressionArtifactsEnabled,
    bool packetLossEnabled,
    bool dataTypeConfusionEnabled,
    bool lackOfClampingEnabled,
    bool memoryAlignmentTearEnabled,
    bool singleChannelTransformEnabled)
{
    Surface stacked = new Surface(src.Width, src.Height);

    for (int y = 0; y < src.Height; y++)
    {
        for (int x = 0; x < src.Width; x++)
        {
            ColorBgra result = src[x, y];
            double amount = intensity;

            for (int i = 0; i < effectOrder.Length; i++)
            {
                result = ApplyEffectById(
                    effectOrder[i],
                    result,
                    src,
                    jpegSurface,
                    null,
                    null,
                    null,
                    stacked,
                    x,
                    y,
                    seed,
                    blockSize,
                    intensity,
                    amount,
                    compressionArtifactsEnabled,
                    packetLossEnabled,
                    dataTypeConfusionEnabled,
                    lackOfClampingEnabled,
                    memoryAlignmentTearEnabled,
                    singleChannelTransformEnabled,
                    false,
                    false,
                    false);
            }

            stacked[x, y] = result;
        }
    }

    return stacked;
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
    bool memoryAlignmentTearEnabled,
    bool singleChannelTransformEnabled,
    bool jpegBinaryDamageEnabled,
    bool pngBinaryDamageEnabled,
    bool gifBinaryDamageEnabled)
{
    int count = 0;
    if (compressionArtifactsEnabled) count++;
    if (packetLossEnabled) count++;
    if (dataTypeConfusionEnabled) count++;
    if (lackOfClampingEnabled) count++;
    if (memoryAlignmentTearEnabled) count++;
    if (singleChannelTransformEnabled) count++;
    if (jpegBinaryDamageEnabled) count++;
    if (pngBinaryDamageEnabled) count++;
    if (gifBinaryDamageEnabled) count++;

    int[] order = new int[count];
    int index = 0;
    if (compressionArtifactsEnabled) order[index++] = 0;
    if (packetLossEnabled) order[index++] = 1;
    if (dataTypeConfusionEnabled) order[index++] = 2;
    if (lackOfClampingEnabled) order[index++] = 3;
    if (memoryAlignmentTearEnabled) order[index++] = 4;
    if (singleChannelTransformEnabled) order[index++] = 5;
    if (jpegBinaryDamageEnabled) order[index++] = 6;
    if (pngBinaryDamageEnabled) order[index++] = 7;
    if (gifBinaryDamageEnabled) order[index++] = 8;

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
    bool memoryAlignmentTearEnabled,
    bool singleChannelTransformEnabled,
    bool jpegBinaryDamageEnabled,
    bool pngBinaryDamageEnabled,
    bool gifBinaryDamageEnabled)
{
    if (effectId == 0 && compressionArtifactsEnabled)
    {
        return JpegBlockPanic(src, jpegSurface, x, y);
    }

    if (effectId == 1 && packetLossEnabled)
    {
        return BlendPreserveAlpha(result, MissingPacketRows(src, x, y, seed, blockSize, intensity), amount * 0.85);
    }

    if (effectId == 2 && dataTypeConfusionEnabled)
    {
        return BlendPreserveAlpha(result, ChannelSwapLab(result, seed, x, y), amount * 0.65);
    }

    if (effectId == 3 && lackOfClampingEnabled)
    {
        return BlendPreserveAlpha(result, LackOfClamping(result, seed, x, y, intensity), amount * 0.75);
    }

    if (effectId == 4 && memoryAlignmentTearEnabled)
    {
        return BlendPreserveAlpha(result, MemoryAlignmentTear(accumulatedResult, x, y, seed, intensity), amount * 0.80);
    }

    if (effectId == 5 && singleChannelTransformEnabled)
    {
        return BlendPreserveAlpha(result, SingleChannelTransform(src, result, x, y, seed), amount * 0.90);
    }

    if (effectId == 6 && jpegBinaryDamageEnabled)
    {
        return JpegBlockPanic(src, damagedJpegSurface, x, y);
    }

    if (effectId == 7 && pngBinaryDamageEnabled)
    {
        return PngBlockCorruption(result, damagedPngSurface, x, y);
    }

    if (effectId == 8 && gifBinaryDamageEnabled)
    {
        return GifBlockCorruption(result, damagedGifSurface, x, y);
    }

    return result;
}

ColorBgra JpegBlockPanic(Surface src, Surface jpegSurface, int x, int y)
{
    ColorBgra jpeg = SampleClamped(jpegSurface, x, y);
    ColorBgra original = SampleClamped(src, x, y);

    // JPEG has no alpha channel, so preserve the source alpha after the real
    // encode/decode pass.
    return ColorBgra.FromBgra(jpeg.B, jpeg.G, jpeg.R, original.A);
}

ColorBgra PngBlockCorruption(ColorBgra current, Surface pngSurface, int x, int y)
{
    if (pngSurface == null)
    {
        return current;
    }

    ColorBgra png = SampleClamped(pngSurface, x, y);
    // PNG supports alpha, so blend with it while preserving overall alpha
    return ColorBgra.FromBgra(png.B, png.G, png.R, current.A);
}

ColorBgra GifBlockCorruption(ColorBgra current, Surface gifSurface, int x, int y)
{
    if (gifSurface == null)
    {
        return current;
    }

    ColorBgra gif = SampleClamped(gifSurface, x, y);
    // GIF supports alpha, so blend with it while preserving overall alpha
    return ColorBgra.FromBgra(gif.B, gif.G, gif.R, current.A);
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
    int trigger = (int)(h % 100);

    if (trigger > (15 + (int)(45.0 * intensity)))
    {
        return src[x, y];
    }

    int direction = ((h & 1u) == 0u) ? -1 : 1;
    int offsetRows = direction * (stripHeight + (int)(h % (uint)(stripHeight * 3)));
    int shift = HashSigned(seed, strip, 0, 29) % Math.Max(1, blockSize * 3);
    int sx = ClampInt(x + shift, 0, src.Width - 1);
    int sy = ClampInt(y + offsetRows, 0, src.Height - 1);
    ColorBgra c = SampleClamped(src, sx, sy);

    if ((h & 4u) != 0u)
    {
        c = ColorBgra.FromBgra(
            (byte)ClampByte(c.B - (int)(40.0 * intensity)),
            (byte)ClampByte(c.G - (int)(28.0 * intensity)),
            (byte)ClampByte(c.R - (int)(12.0 * intensity)),
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

ColorBgra MemoryAlignmentTear(Surface src, int x, int y, int seed, double intensity)
{
    int tear = 1 + (int)(5.0 * intensity);
    int wobble = HashSigned(seed, x / 16, y / 16, 53) % tear;

    ColorBgra left = SampleClamped(src, x - tear + wobble, y);
    ColorBgra mid = SampleClamped(src, x, y);
    ColorBgra right = SampleClamped(src, x + tear + wobble, y);

    return ColorBgra.FromBgra(right.B, mid.G, left.R, mid.A);
}

ColorBgra SingleChannelTransform(Surface src, ColorBgra current, int x, int y, int seed)
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
