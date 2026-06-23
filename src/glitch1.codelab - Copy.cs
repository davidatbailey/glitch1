// glitch1 - Paint.NET CodeLab ClassicEffect script
//
// This script is structured as a coding exemplar: deterministic coordinate
// hashing, source-only sampling, alpha preservation, and minimal per-pixel work.
// In CodeLab, use/accept the ClassicEffect project type for this Surface API.

#region UICode
IntSliderControl Amount1 = 501; // [1,10000] Seed number
CheckboxControl Amount2 = true; // Allow JPEG Block Panic
CheckboxControl Amount3 = true; // Allow Missing Packets
CheckboxControl Amount4 = true; // Allow Channel Swap
CheckboxControl Amount5 = true; // Allow Lack Of Clamping
CheckboxControl Amount6 = true; // Allow Memory Alignment Tear
CheckboxControl Amount7 = true; // Allow Single Channel Transform
IntSliderControl Amount8 = 0; // [0,100] JPEG Binary Damage intensity
CheckboxControl Amount9 = true; // Stack effects
CheckboxControl Amount10 = false; // Pre-rotate for directional variation
#endregion

void Render(Surface dst, Surface src, Rectangle rect)
{
    int seed = Amount1;
    bool jpegBlockPanicEnabled = Amount2 && SeedSelectEffect(seed, 0);
    bool missingPacketsEnabled = Amount3 && SeedSelectEffect(seed, 1);
    bool channelSwapEnabled = Amount4 && SeedSelectEffect(seed, 2);
    bool lackOfClampingEnabled = Amount5 && SeedSelectEffect(seed, 3);
    bool memoryAlignmentTearEnabled = Amount6 && SeedSelectEffect(seed, 4);
    bool singleChannelTransformEnabled = Amount7 && SeedSelectEffect(seed, 5);
    bool jpegBinaryDamageEnabled = Amount8 > 0 && SeedSelectEffect(seed, 6);
    bool preRotateEnabled = Amount10;
    
    int rotationMode = 0;
    Surface rotatedSrc = src;
    if (preRotateEnabled)
    {
        rotationMode = (int)(Hash(seed, 0, 0, 241) % 8u);
        rotatedSrc = ApplyPreRotation(src, rotationMode);
    }
    if (!jpegBlockPanicEnabled &&
        !missingPacketsEnabled &&
        !channelSwapEnabled &&
        !lackOfClampingEnabled &&
        !memoryAlignmentTearEnabled &&
        !singleChannelTransformEnabled &&
        !jpegBinaryDamageEnabled)
    {
        jpegBlockPanicEnabled = Amount2;
        missingPacketsEnabled = !jpegBlockPanicEnabled && Amount3;
        channelSwapEnabled = !jpegBlockPanicEnabled && !missingPacketsEnabled && Amount4;
        lackOfClampingEnabled = !jpegBlockPanicEnabled && !missingPacketsEnabled && !channelSwapEnabled && Amount5;
        memoryAlignmentTearEnabled = !jpegBlockPanicEnabled && !missingPacketsEnabled && !channelSwapEnabled && !lackOfClampingEnabled && Amount6;
        singleChannelTransformEnabled = !jpegBlockPanicEnabled && !missingPacketsEnabled && !channelSwapEnabled && !lackOfClampingEnabled && !memoryAlignmentTearEnabled && Amount7;
        jpegBinaryDamageEnabled = !jpegBlockPanicEnabled && !missingPacketsEnabled && !channelSwapEnabled && !lackOfClampingEnabled && !memoryAlignmentTearEnabled && !singleChannelTransformEnabled && Amount8 > 0;
    }
    bool stackEffects = Amount9;
    double intensity = 0.45 + (Random01(seed, 0, 0, 101) * 0.45);
    int blockSize = 4 + ((int)(Hash(seed, 0, 0, 103) % 8u) * 2);
    double maskAmount = 0.35 + (Random01(seed, 0, 0, 107) * 0.55);
    double maskScale = 12.0 + (Random01(seed, 0, 0, 109) * 54.0);
    double maskPhase = Random01(seed, 0, 0, 113);
    int jpegQuality = GetJpegQuality(seed, intensity);
    Surface jpegSurface = null;
    Surface damagedJpegSurface = null;
    PrepareJpegSurfaces(rotatedSrc, jpegQuality, seed, jpegBlockPanicEnabled, out jpegSurface);
    int[] effectOrder = GetEffectOrder(
        seed,
        stackEffects,
        jpegBlockPanicEnabled,
        missingPacketsEnabled,
        channelSwapEnabled,
        lackOfClampingEnabled,
        memoryAlignmentTearEnabled,
        singleChannelTransformEnabled,
        false);
    bool needsMask = missingPacketsEnabled || channelSwapEnabled || lackOfClampingEnabled || memoryAlignmentTearEnabled || singleChannelTransformEnabled;

    if (jpegBinaryDamageEnabled)
    {
        Surface stackedSurface = RenderStackToSurface(
            rotatedSrc,
            jpegSurface,
            effectOrder,
            seed,
            blockSize,
            intensity,
            maskAmount,
            maskScale,
            maskPhase,
            needsMask,
            jpegBlockPanicEnabled,
            missingPacketsEnabled,
            channelSwapEnabled,
            lackOfClampingEnabled,
            memoryAlignmentTearEnabled,
            singleChannelTransformEnabled);
        double damageIntensity = Amount8 / 100.0;
        damagedJpegSurface = CreateJpegBinaryDamageSurface(stackedSurface, jpegQuality, seed, damageIntensity);
    }

    if (jpegBinaryDamageEnabled)
    {
        for (int y = rect.Top; y < rect.Bottom; y++)
        {
            for (int x = rect.Left; x < rect.Right; x++)
            {
                if (x < rotatedSrc.Width && y < rotatedSrc.Height)
                {
                    ColorBgra result = JpegBlockPanic(rotatedSrc, damagedJpegSurface, x, y);
                    if (preRotateEnabled)
                    {
                        int dstX, dstY;
                        ReversePreRotationCoords(x, y, src.Width, src.Height, rotatedSrc.Width, rotatedSrc.Height, rotationMode, out dstX, out dstY);
                        if (dstX >= 0 && dstX < src.Width && dstY >= 0 && dstY < src.Height)
                        {
                            dst[dstX, dstY] = result;
                        }
                    }
                    else
                    {
                        dst[x, y] = result;
                    }
                }
            }
        }

        return;
    }

    for (int y = rect.Top; y < rect.Bottom; y++)
    {
        for (int x = rect.Left; x < rect.Right; x++)
        {
            ColorBgra original = rotatedSrc[x, y];
            ColorBgra result = original;
            double amount = intensity;
            if (needsMask)
            {
                double mask = GetMaskValue(x, y, rotatedSrc.Width, rotatedSrc.Height, seed, maskScale, maskPhase);
                amount *= (1.0 - maskAmount) + (maskAmount * mask);
            }

            for (int i = 0; i < effectOrder.Length; i++)
            {
                result = ApplyEffectById(
                    effectOrder[i],
                    result,
                    rotatedSrc,
                    jpegSurface,
                    damagedJpegSurface,
                    x,
                    y,
                    seed,
                    blockSize,
                    intensity,
                    amount,
                    jpegBlockPanicEnabled,
                    missingPacketsEnabled,
                    channelSwapEnabled,
                    lackOfClampingEnabled,
                    memoryAlignmentTearEnabled,
                    singleChannelTransformEnabled,
                    false);
            }

            if (preRotateEnabled)
            {
                int dstX, dstY;
                ReversePreRotationCoords(x, y, src.Width, src.Height, rotatedSrc.Width, rotatedSrc.Height, rotationMode, out dstX, out dstY);
                if (dstX >= 0 && dstX < src.Width && dstY >= 0 && dstY < src.Height)
                {
                    dst[dstX, dstY] = result;
                }
            }
            else
            {
                dst[x, y] = result;
            }
        }
    }
}

bool SeedSelectEffect(int seed, int effectId)
{
    uint roll = Hash(seed, effectId, 0, 239) % 100u;
    if (effectId == 0)
    {
        return roll < 55u;
    }

    if (effectId == 6)
    {
        return roll < 35u;
    }

    return roll < 45u;
}

Surface RenderStackToSurface(
    Surface src,
    Surface jpegSurface,
    int[] effectOrder,
    int seed,
    int blockSize,
    double intensity,
    double maskAmount,
    double maskScale,
    double maskPhase,
    bool needsMask,
    bool jpegBlockPanicEnabled,
    bool missingPacketsEnabled,
    bool channelSwapEnabled,
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
            if (needsMask)
            {
                double mask = GetMaskValue(x, y, src.Width, src.Height, seed, maskScale, maskPhase);
                amount *= (1.0 - maskAmount) + (maskAmount * mask);
            }

            for (int i = 0; i < effectOrder.Length; i++)
            {
                result = ApplyEffectById(
                    effectOrder[i],
                    result,
                    src,
                    jpegSurface,
                    null,
                    x,
                    y,
                    seed,
                    blockSize,
                    intensity,
                    amount,
                    jpegBlockPanicEnabled,
                    missingPacketsEnabled,
                    channelSwapEnabled,
                    lackOfClampingEnabled,
                    memoryAlignmentTearEnabled,
                    singleChannelTransformEnabled,
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
    bool jpegBlockPanicEnabled,
    out Surface jpegSurface)
{
    jpegSurface = null;

    if (!jpegBlockPanicEnabled)
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
    bool jpegBlockPanicEnabled,
    bool missingPacketsEnabled,
    bool channelSwapEnabled,
    bool lackOfClampingEnabled,
    bool memoryAlignmentTearEnabled,
    bool singleChannelTransformEnabled,
    bool jpegBinaryDamageEnabled)
{
    int count = 0;
    if (jpegBlockPanicEnabled) count++;
    if (missingPacketsEnabled) count++;
    if (channelSwapEnabled) count++;
    if (lackOfClampingEnabled) count++;
    if (memoryAlignmentTearEnabled) count++;
    if (singleChannelTransformEnabled) count++;
    if (jpegBinaryDamageEnabled) count++;

    int[] order = new int[count];
    int index = 0;
    if (jpegBlockPanicEnabled) order[index++] = 0;
    if (missingPacketsEnabled) order[index++] = 1;
    if (channelSwapEnabled) order[index++] = 2;
    if (lackOfClampingEnabled) order[index++] = 3;
    if (memoryAlignmentTearEnabled) order[index++] = 4;
    if (singleChannelTransformEnabled) order[index++] = 5;
    if (jpegBinaryDamageEnabled) order[index++] = 6;

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
    int x,
    int y,
    int seed,
    int blockSize,
    double intensity,
    double amount,
    bool jpegBlockPanicEnabled,
    bool missingPacketsEnabled,
    bool channelSwapEnabled,
    bool lackOfClampingEnabled,
    bool memoryAlignmentTearEnabled,
    bool singleChannelTransformEnabled,
    bool jpegBinaryDamageEnabled)
{
    if (effectId == 0 && jpegBlockPanicEnabled)
    {
        return JpegBlockPanic(src, jpegSurface, x, y);
    }

    if (effectId == 1 && missingPacketsEnabled)
    {
        return BlendPreserveAlpha(result, MissingPacketRows(src, x, y, seed, blockSize, intensity), amount * 0.85);
    }

    if (effectId == 2 && channelSwapEnabled)
    {
        return BlendPreserveAlpha(result, ChannelSwapLab(result, seed, x, y), amount * 0.65);
    }

    if (effectId == 3 && lackOfClampingEnabled)
    {
        return BlendPreserveAlpha(result, LackOfClamping(result, seed, x, y, intensity), amount * 0.60);
    }

    if (effectId == 4 && memoryAlignmentTearEnabled)
    {
        return BlendPreserveAlpha(result, MemoryAlignmentTear(src, x, y, seed, intensity), amount * 0.80);
    }

    if (effectId == 5 && singleChannelTransformEnabled)
    {
        return BlendPreserveAlpha(result, SingleChannelTransform(src, result, x, y, seed), amount * 0.90);
    }

    if (effectId == 6 && jpegBinaryDamageEnabled)
    {
        return JpegBlockPanic(src, damagedJpegSurface, x, y);
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

    int mode = (int)(Hash(seed, 0, 0, 181) % 3u);
    if (mode == 0)
    {
        byte[] damaged = CopyBytes(source);
        int edits = (int)((12 + (int)(Random01(seed, 0, 0, 183) * 36.0)) * damageIntensity);
        int span = dataEnd - dataStart;
        for (int i = 0; i < edits; i++)
        {
            int index = dataStart + (int)(Hash(seed, i, 0, 187) % (uint)span);
            if (damaged[index] != 255)
            {
                damaged[index] = (byte)(damaged[index] ^ (byte)(1 + (Hash(seed, i, 0, 191) % 254u)));
            }
        }

        return damaged;
    }

    if (mode == 1)
    {
        int span = dataEnd - dataStart;
        int maxLength = Math.Max(1, span / 3);
        int length = ClampInt(8 + (int)(Random01(seed, 0, 0, 193) * 64.0), 1, maxLength);
        length = (int)(length * damageIntensity);
        length = Math.Max(1, length);
        int from = dataStart + (int)(Hash(seed, 0, 0, 197) % (uint)(span - length));
        int insertAt = dataStart + (int)(Hash(seed, 0, 0, 199) % (uint)span);
        byte[] damaged = new byte[source.Length + length];
        System.Array.Copy(source, 0, damaged, 0, insertAt);
        System.Array.Copy(source, from, damaged, insertAt, length);
        System.Array.Copy(source, insertAt, damaged, insertAt + length, source.Length - insertAt);
        return damaged;
    }

    int removeSpan = dataEnd - dataStart;
    int maxRemoveLength = Math.Max(1, removeSpan / 4);
    int removeLength = ClampInt(8 + (int)(Random01(seed, 0, 0, 211) * 48.0), 1, maxRemoveLength);
    removeLength = (int)(removeLength * damageIntensity);
    removeLength = Math.Max(1, removeLength);
    int removeAt = dataStart + (int)(Hash(seed, 0, 0, 223) % (uint)(removeSpan - removeLength));
    byte[] removed = new byte[source.Length - removeLength];
    System.Array.Copy(source, 0, removed, 0, removeAt);
    System.Array.Copy(source, removeAt + removeLength, removed, removeAt, source.Length - removeAt - removeLength);
    return removed;
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
    int noise = HashSigned(seed, x / 4, y / 4, 43);
    int push = (int)(90.0 * intensity);

    int r = WrapByte(c.R + push + (noise % 64));
    int g = WrapByte(c.G - push + ((noise >> 3) % 96));
    int b = WrapByte(c.B + (push * 2) - ((noise >> 5) % 128));

    if ((noise & 8) != 0)
    {
        int spill = r;
        r = WrapByte(g + b);
        g = WrapByte(b + spill);
        b = WrapByte(spill + g);
    }

    return ColorBgra.FromBgra((byte)b, (byte)g, (byte)r, c.A);
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
        sx = ClampInt((int)(cx + (dx * stretch)), 0, src.Width - 1);
        sy = ClampInt((int)(cy + dy), 0, src.Height - 1);
        return;
    }

    if (transform == 1)
    {
        double scale = 0.70 + (Random01(seed, 0, 0, 167) * 0.75);
        sx = ClampInt((int)(cx + (dx / scale)), 0, src.Width - 1);
        sy = ClampInt((int)(cy + (dy / scale)), 0, src.Height - 1);
        return;
    }

    double angle = (Random01(seed, 0, 0, 173) - 0.5) * 0.70;
    double ca = Math.Cos(angle);
    double sa = Math.Sin(angle);
    sx = ClampInt((int)(cx + (dx * ca) - (dy * sa)), 0, src.Width - 1);
    sy = ClampInt((int)(cy + (dx * sa) + (dy * ca)), 0, src.Height - 1);
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

double GetMaskValue(int x, int y, int width, int height, int seed, double scale, double phase)
{
    double nx = width <= 1 ? 0.0 : (double)x / (double)(width - 1);
    double ny = height <= 1 ? 0.0 : (double)y / (double)(height - 1);
    double p = phase * 6.283185307179586;
    double s = 2.0 + (scale * 0.12);

    double waveA = Math.Sin(((nx * s) + phase) * 6.283185307179586);
    double waveB = Math.Sin(((ny * (s * 0.73)) - phase) * 6.283185307179586);
    double waveC = Math.Sin((((nx + ny) * (s * 0.41)) + (p * 0.15915494309189535)) * 6.283185307179586);

    int cellX = (int)(nx * (8.0 + scale * 0.35));
    int cellY = (int)(ny * (8.0 + scale * 0.35));
    double turbulence = Random01(seed, cellX, cellY, 71) - 0.5;

    double value = (waveA * 0.34) + (waveB * 0.28) + (waveC * 0.23) + (turbulence * 0.30);
    value = (value + 1.0) * 0.5;

    if (value < 0.0) return 0.0;
    if (value > 1.0) return 1.0;
    return value * value * (3.0 - (2.0 * value));
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
