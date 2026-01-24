using System;
using System.Collections.Generic;
using System.Text;

namespace AstcEncoder
{
    /// <summary>
    /// Astcenc config creation flags.
    /// </summary>
    public enum AstcencFlags : uint
    {
        /// <summary>
        /// Enable normal map compression.
        /// <remarks>
        /// Input data will be treated a two component normal map, storing X and Y, and the codec will
        /// optimize for angular error rather than simple linear PSNR. In this mode the input swizzle should
        /// be e.g. rrrg (the default ordering for ASTC normals on the command line) or gggr (the ordering
        /// used by BC5n).
        /// </remarks>
        /// </summary>
        MapNormal = 1 << 0,
        /// <summary>
        /// Enable compression heuristics that assume use of decode_unorm8 decode mode.
        /// </summary>
        /// <remarks>
        /// The decode_unorm8 decode mode rounds differently to the decode_fp16 decode mode, so
        /// enabling this flag during compression will allow the compressor to use the correct
        /// rounding when selecting encodings. This will improve the compressed image quality
        /// if your application is using the decode_unorm8 decode mode, but will reduce image
        /// quality if using decode_fp16.
        ///
        /// Note that LDR_SRGB images will always use decode_unorm8 for the RGB channels,
        /// irrespective of this setting.
        /// </remarks>
        UseDecodeUnorm8 = 1 << 1,
        /// <summary>
        /// Enable alpha weighting.
        /// </summary>
        /// <remarks>
        /// The input alpha value is used for transparency, so errors in the RGB components are
        /// weighted by the transparency level. This allows the codec to more accurately encode
        /// the alpha value in areas where the color value is less significant.
        /// </remarks>
        UseAlphaWeight = 1 << 2,
        /// <summary>
        /// Enable perceptual error metrics.
        /// </summary>
        /// <remarks>
        /// This mode enables perceptual compression mode, which will optimize for perceptual
        /// error rather than best PSNR. Only some input modes support perceptual error metrics.
        /// </remarks>
        UsePerceptual = 1 << 3,
        /// <summary>
        /// Create a decompression-only context.
        /// </summary>
        /// <remarks>
        /// This mode disables support for compression. This enables context allocation to skip
        /// some transient buffer allocation, resulting in lower memory usage.
        /// </remarks>
        DecompressOnly = 1 << 4,
        /// <summary>
        /// Create a self-decompression context.
        /// </summary>
        /// <remarks>
        /// This mode configures the compressor so that it is only guaranteed to be able to
        /// decompress images that were actually created using the current context. This is
        /// the common case for compression use cases, and setting this flag enables additional
        /// optimizations, but does mean that the context cannot reliably decompress arbitrary
        /// ASTC images.
        /// </remarks>
        SelfDecompressOnly = 1 << 5,
        /// <summary>
        /// Enable RGBM map compression.
        /// </summary>
        /// <remarks>
        /// Input data will be treated as HDR data that has been stored in an LDR RGBM-encoded
        /// wrapper format. Data must be preprocessed by the user to be in LDR RGBM format before
        /// calling the compression function; this flag is only used to control the use of
        /// RGBM-specific heuristics and error metrics.
        ///
        /// <para>
        /// IMPORTANT: The ASTC format is prone to bad failure modes with unconstrained RGBM
        /// data; very small M values can round to zero due to quantization and result in black
        /// or white pixels. It is highly recommended that the minimum value of M used in the
        /// encoding is kept above a lower threshold (try 16 or 32). Applying this threshold
        /// reduces the number of very dark colors that can be represented, but is still higher
        /// precision than 8-bit LDR.
        /// </para>
        ///
        /// <para>
        /// When this flag is set the value of rgbm_m_scale in the context must be set to the RGBM
        /// scale factor used during reconstruction. This defaults to 5 when in RGBM mode.
        /// </para>
        ///
        /// <para>
        /// It is recommended that the value of cw_a_weight is set to twice the value of the
        /// multiplier scale, ensuring that the M value is accurately encoded. This defaults to
        /// 10 when in RGBM mode, matching the default scale factor.
        /// </para>
        /// </remarks>
        MapRgbm = 1 << 6,
        /// <summary>
        /// The bit mask of all valid flags.
        /// </summary>
        All = MapNormal | MapRgbm | UseAlphaWeight | UsePerceptual | UseDecodeUnorm8 | DecompressOnly | SelfDecompressOnly,
    }
}
