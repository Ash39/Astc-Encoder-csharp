using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace AstcEncoder
{
    public partial class Astcenc
    {
        static Astcenc()
        {
            NativeLibrary.SetDllImportResolver(typeof(Astcenc).Assembly, ImportResolver);
        }

        private static nint ImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (libraryName == "astcenc")
                return LoadAstcencLibrary(assembly, searchPath);
            return NativeLibrary.Load(libraryName, assembly, searchPath);
        }

        private static nint LoadAstcencLibrary(Assembly assembly, DllImportSearchPath? searchPath)
        {
            string libraryFileName = AstcencLibraryName;

            foreach (string candidatePath in GetNativeLibraryCandidatePaths(assembly, libraryFileName))
            {
                if (NativeLibrary.TryLoad(candidatePath, out nint handle))
                {
                    return handle;
                }
            }

            return NativeLibrary.Load(libraryFileName, assembly, searchPath);
        }

        private static IEnumerable<string> GetNativeLibraryCandidatePaths(Assembly assembly, string libraryFileName)
        {
            string? assemblyDirectory = Path.GetDirectoryName(assembly.Location);
            if (string.IsNullOrEmpty(assemblyDirectory))
            {
                yield break;
            }

            yield return Path.Combine(assemblyDirectory, libraryFileName);

            foreach (string runtimeRid in GetRuntimeIdentifiers())
            {
                yield return Path.Combine(assemblyDirectory, runtimeRid, "native", libraryFileName);
                yield return Path.Combine(assemblyDirectory, "runtimes", runtimeRid, "native", libraryFileName);
            }
        }

        private static IEnumerable<string> GetRuntimeIdentifiers()
        {
            if (OperatingSystem.IsWindows())
            {
                if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                {
                    yield return "win-arm64";
                    yield break;
                }

                if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
                {
                    yield return "win-x64";
                    yield break;
                }

                yield return "win-x86";
                yield break;
            }

            if (OperatingSystem.IsLinux())
            {
                if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                {
                    yield return "linux-arm64";
                    yield break;
                }

                yield return "linux-x64";
                yield break;
            }

            if (OperatingSystem.IsMacOS())
            {
                yield return "osx";

                if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                {
                    yield return "osx-arm64";
                    yield return "osx-x64";
                    yield break;
                }

                yield return "osx-x64";
                yield return "osx-arm64";
            }
        }

        private static string AstcencLibraryName 
        {
            get
            {
                string libraryFile = string.Empty;
                if (OperatingSystem.IsWindows())
                {
                    libraryFile = RuntimeInformation.ProcessArchitecture switch
                    {
                        Architecture.X64 => IsAstcencAvx2Supported ? "astcenc-avx2-shared.dll" :
                            IsAstcencSSE41Supported ? "astcenc-sse4.1-shared.dll" :
                            IsAstcencSSE2Supported ? "astcenc-sse2-shared.dll" :
                            throw new PlatformNotSupportedException(
                                "The required CPU instructions for x64 architecture are not supported."),
                        Architecture.Arm64 => IsAstcencSve256Supported ? "astcenc-arm-sve256-shared.dll" :
                            IsAstcencSve128Supported ? "astcenc-arm-sve128-shared.dll" :
                            IsAstcencNeonSupported ? "astcenc-arm-neon-shared.dll" :
                            throw new PlatformNotSupportedException(
                                "The required CPU instructions for ARM architecture are not supported."),
                        _ => throw new PlatformNotSupportedException("Unsupported architecture"),
                    };
                }
                else if (OperatingSystem.IsLinux())
                {
                    libraryFile = RuntimeInformation.ProcessArchitecture switch
                    {
                        Architecture.X64 => IsAstcencAvx2Supported ? "libastcenc-avx2-shared.so" :
                            IsAstcencSSE41Supported ? "libastcenc-sse4.1-shared.so" :
                            IsAstcencSSE2Supported ? "libastcenc-sse2-shared.so" :
                            throw new PlatformNotSupportedException(
                                "The required CPU instructions for x64 architecture are not supported."),
                        Architecture.Arm64 => IsAstcencSve256Supported ? "libastcenc-sve256-shared.so" :
                            IsAstcencSve128Supported ? "libastcenc-sve128-shared.so" :
                            IsAstcencNeonSupported ? "libastcenc-neon-shared.so" :
                            throw new PlatformNotSupportedException(
                                "The required CPU instructions for ARM architecture are not supported."),
                        _ => throw new PlatformNotSupportedException("Unsupported architecture"),
                    };
                }
                else if (OperatingSystem.IsMacOS())
                {
                    libraryFile = "libastcenc-shared.dylib";
                }
                else
                {
                    throw new PlatformNotSupportedException("Unsupported platform");
                }
                
                return libraryFile;
            }
        }

        private static bool IsAstcencSSE2Supported => IsSupported(CpuInstruction.SSE2);
        private static bool IsAstcencSSE41Supported => IsSupported(CpuInstruction.SSE4_1) 
                                             && IsSupported(CpuInstruction.POPCNT);
        private static bool IsAstcencAvx2Supported => IsSupported(CpuInstruction.AVX2) 
                                            && IsSupported(CpuInstruction.SSE4_2) 
                                            && IsSupported(CpuInstruction.POPCNT) 
                                            && IsSupported(CpuInstruction.F16C);

        private static bool IsAstcencSve128Supported => IsSupported(CpuInstruction.SVEC_128);
        private static bool IsAstcencSve256Supported => IsSupported(CpuInstruction.SVEC_256);
        private static bool IsAstcencNeonSupported => IsSupported(CpuInstruction.NEON);

        private static bool IsSupported(CpuInstruction instruction) 
        {
            switch (RuntimeInformation.ProcessArchitecture)
            {
                case Architecture.X86:
                case Architecture.X64:
                    switch (instruction)
                    {
                        case CpuInstruction.SSE2:
                            return Sse2.IsSupported;
                        case CpuInstruction.SSE4_1:
                            return Sse41.IsSupported;
                        case CpuInstruction.SSE4_2:
                            return Sse42.IsSupported;
                        case CpuInstruction.POPCNT:
                            return Popcnt.IsSupported;
                        case CpuInstruction.AVX2:
                        case CpuInstruction.F16C:
                            return Avx2.IsSupported;
                        default:
                            throw new NotSupportedException("Unsupported instruction for x86/x64 architecture");
                    }
                case Architecture.Arm:
                case Architecture.Arm64:
                case Architecture.Armv6:
                    switch (instruction)
                    {
                        case CpuInstruction.SVEC_128:
#pragma warning disable SYSLIB5003 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                            return Sve.IsSupported && Vector128.IsHardwareAccelerated;
                        case CpuInstruction.SVEC_256:
                            return Sve.IsSupported && Vector256.IsHardwareAccelerated;
#pragma warning restore SYSLIB5003 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
                        case CpuInstruction.NEON:
                            return AdvSimd.IsSupported;
                        default:
                            throw new NotSupportedException("Unsupported instruction for ARM architecture");
                    }
                default:
                    throw new NotSupportedException("Unsupported architecture");
            }
            
        }


        /// <summary>
        /// Populate a codec config based on default settings.
        /// </summary>
        /// <remarks>
        /// Power users can edit the returned config struct to fine tune before allocating the context.
        /// </remarks>
        /// <param name="profile">Color profile.</param>
        /// <param name="blockX">ASTC block size X dimension.</param>
        /// <param name="blockY">ASTC block size Y dimension.</param>
        /// <param name="blockZ">ASTC block size Z dimension.</param>
        /// <param name="quality">
        /// Search quality preset / effort level. Either an ASTCENC_PRE_* value,
        /// or an effort level between 0 and 100. Performance is not linear between 0 and 100.
        /// </param>
        /// <param name="flags">A valid set of ASTCENC_FLG_* flag bits.</param>
        /// <param name="config">(out) Output config struct to populate.</param>
        /// <returns>
        /// AstcencSuccess on success, or an error if the inputs are invalid either individually,
        /// or in combination.
        /// </returns>
        public unsafe static AstcencError AstcencConfigInit(AstcencProfile profile, uint blockX, uint blockY, uint blockZ, float quality, AstcencFlags flags, out AstcencConfig config)
        { 
            AstcencConfig configRef;

            AstcencError status = AstcencUnmanaged.AstcencConfigInit(profile, blockX, blockY, blockZ, quality, (uint)flags, &configRef);

            config = Unsafe.AsRef<AstcencConfig>(ref configRef);

            return status;
        }

        /// <summary>
        /// Allocate a new codec context based on a config.
        /// </summary>
        /// <remarks>
        /// This function allocates all of the memory resources and threads needed by the codec.
        /// This can be slow, so it is recommended that contexts are reused to serially compress or
        /// decompress multiple images to amortize setup cost.
        ///
        /// Contexts can be allocated to support only decompression using the
        /// ASTCENC_FLG_DECOMPRESS_ONLY flag when creating the configuration. The compression
        /// functions will fail if invoked. For a decompress-only library build the
        /// ASTCENC_FLG_DECOMPRESS_ONLY flag must be set when creating any context.
        /// </remarks>
        /// <param name="config">(in) Codec config.</param>
        /// <param name="threadCount">Thread count to configure for.</param>
        /// <param name="context">(out) Location to store an opaque context pointer.</param>
        /// <returns>
        /// AstcencSuccess on success, or an error if context creation failed.
        /// </returns>
        public unsafe static AstcencError AstcencContextAlloc(ref AstcencConfig config, uint threadCount, out AstcencContext context) 
        {
            AstcencContextInternal* astcencContext;

            AstcencError status = AstcencUnmanaged.AstcencContextAlloc((AstcencConfig*)Unsafe.AsPointer(ref config), threadCount, &astcencContext);

            context.internal_context = (IntPtr)astcencContext;

            return status;
        }

        /// <summary>
        /// Compress an image.
        /// </summary>
        /// <remarks>
        /// A single context can only compress or decompress a single image at a time.
        ///
        /// For a context configured for multi-threading, any set of the N threads can call this
        /// function. Work will be dynamically scheduled across the threads available. Each thread
        /// must have a unique thread_index.
        /// </remarks>
        /// <param name="context">Codec context.</param>
        /// <param name="image">(in,out) An input image, in 2D slices.</param>
        /// <param name="swizzle">Compression data swizzle, applied before compression.</param>
        /// <param name="dataOut">(out) Span to output data array.</param>
        /// <param name="threadIndex">Thread index [0..N-1] of calling thread.</param>
        /// <returns>
        /// AstcencSuccess on success, or an error if compression failed.
        /// </returns>
        public unsafe static AstcencError AstcencCompressImage(AstcencContext context, ref AstcencImage image, AstcencSwizzle swizzle, Span<byte> dataOut, uint threadIndex)
        {
            fixed (void* dataPtr = image.data) 
            {
                fixed (byte* outDataPtr = dataOut)
                {
                    Unsafe.SkipInit(out AstcencImageInternal nativeImage);
                    nativeImage.dimX = image.dimX;
                    nativeImage.dimY = image.dimY;
                    nativeImage.dimZ = image.dimZ;
                    nativeImage.dataType = image.dataType;
                    nativeImage.data = &dataPtr;

                    return AstcencUnmanaged.AstcencCompressImage((AstcencContextInternal*)context.internal_context, &nativeImage, &swizzle, outDataPtr, (uint)dataOut.Length, threadIndex);
                }
            }
        }

        /// <summary>
        /// Reset the codec state for a new compression.
        /// </summary>
        /// <remarks>
        /// The caller is responsible for synchronizing threads in the worker thread pool. This
        /// function must only be called when all threads have exited the
        /// astcenc_compress_image function for image N, but before any thread enters it for
        /// image N + 1.
        ///
        /// Calling this is not required (but won't hurt), if the context is created for
        /// single threaded use.
        /// </remarks>
        /// <param name="context">Codec context.</param>
        /// <returns>
        /// AstcencSuccess on success, or an error if reset failed.
        /// </returns>
        public unsafe static AstcencError AstcencCompressReset(AstcencContext context) =>
            AstcencUnmanaged.AstcencCompressReset((AstcencContextInternal*)context.internal_context);

        /// <summary>
        /// Cancel any pending compression operation.
        /// </summary>
        /// <remarks>
        /// The caller must behave as if the compression completed normally, even though the data
        /// will be undefined. They are still responsible for synchronizing threads in the worker
        /// thread pool, and must call reset before starting another compression.
        /// </remarks>
        /// <param name="context">Codec context.</param>
        /// <returns>
        /// AstcencSuccess on success, or an error if cancellation failed.
        /// </returns>
        public unsafe static AstcencError AstcencCompressCancel(AstcencContext context)=>
            AstcencUnmanaged.AstcencCompressCancel((AstcencContextInternal*)context.internal_context);

        /// <summary>
        /// Decompress an image.
        /// </summary>
        /// <param name="context">Codec context.</param>
        /// <param name="data">(in) Span to compressed data.</param>
        /// <param name="imageOut">(in,out) Output image.</param>
        /// <param name="swizzle">Decompression data swizzle, applied after decompression.</param>
        /// <param name="threadIndex">Thread index [0..N-1] of calling thread.</param>
        /// <returns>
        /// AstcencSuccess on success, or an error if decompression failed.
        /// </returns>
        public unsafe static AstcencError AstcencDecompressImage(AstcencContext context, Span<byte> data, ref AstcencImage imageOut, AstcencSwizzle swizzle, uint threadIndex) 
        {
            fixed (byte* dataPtr = data)
            {
                fixed (void* outDataPtr = imageOut.data)
                {
                    Unsafe.SkipInit(out AstcencImageInternal nativeImage);
                    nativeImage.dimX = imageOut.dimX;
                    nativeImage.dimY = imageOut.dimY;
                    nativeImage.dimZ = imageOut.dimZ;
                    nativeImage.dataType = imageOut.dataType;
                    nativeImage.data = &outDataPtr;

                    return AstcencUnmanaged.AstcencDecompressImage((AstcencContextInternal*)context.internal_context, dataPtr, (uint)data.Length, &nativeImage, &swizzle, threadIndex);
                }
            }
        }
        /// <summary>
        /// Reset the codec state for a new decompression.
        /// </summary>
        /// <remarks>
        /// The caller is responsible for synchronizing threads in the worker thread pool. This
        /// function must only be called when all threads have exited the
        /// astcenc_decompress_image function for image N, but before any thread enters it for
        /// image N + 1.
        ///
        /// Calling this is not required (but won't hurt), if the context is created for
        /// single threaded use.
        /// </remarks>
        /// <param name="context">Codec context.</param>
        /// <returns>
        /// AstcencSuccess on success, or an error if reset failed.
        /// </returns>
        public unsafe static AstcencError AstcencDecompressReset(AstcencContext context) =>
            AstcencUnmanaged.AstcencDecompressReset((AstcencContextInternal*)context.internal_context);

        /// <summary>
        /// Free the compressor context.
        /// </summary>
        /// <param name="context">The codec context.</param>
        public unsafe static void AstcencContextFree(AstcencContext context) =>
            AstcencUnmanaged.AstcencContextFree((AstcencContextInternal*)context.internal_context);

        /// <summary>
        /// Provide a high level summary of a block's encoding.
        /// </summary>
        /// <remarks>
        /// This feature is primarily useful for codec developers but may be useful for developers
        /// building advanced content packaging pipelines.
        /// </remarks>
        /// <param name="context">Codec context.</param>
        /// <param name="data">One block of compressed ASTC data.</param>
        /// <param name="info">The output info structure to populate.</param>
        /// <returns>
        /// AstcencSuccess if the block was decoded, or an error otherwise. Note that this
        /// function will return success even if the block itself was an error block encoding,
        /// as the decode was correctly handled.
        /// </returns>
        public unsafe static AstcencError AstcencGetBlockInfo(AstcencContext context, Span<byte> data, out AstcencBlockInfo info) 
        {
            AstcencBlockInfo astcencBlockInfo;

            if (data.Length > 16)
            {
                throw new OutOfMemoryException($"Data supplied is larger than required bytes by {data.Length - 16} bytes.");
            }

            fixed (void* ptr = data) 
            {
                AstcencError status = AstcencUnmanaged.AstcencGetBlockInfo((AstcencContextInternal*)context.internal_context, (IntPtr)ptr, &astcencBlockInfo);

                info = astcencBlockInfo;

                return status;
            }
            
        }

        /// <summary>
        /// Get a printable string for a specific status code.
        /// </summary>
        /// <param name="status">The status value.</param>
        /// <returns>A human readable null-terminated string.</returns>
        public unsafe static string? GetErrorString(AstcencError status) 
        {
            return Marshal.PtrToStringAnsi((IntPtr)(void*)AstcencUnmanaged.AstcencGetErrorString(status));
        }
    }
}
