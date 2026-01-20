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

namespace Astc_Encoder_CSharp
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
                return NativeLibrary.Load(AstcencLibraryName, assembly, searchPath);
            return NativeLibrary.Load(libraryName, assembly, searchPath);
        }

        private static string AstcencLibraryName 
        {
            get 
            {
                // Determine the library file name first (same logic as before).
                string libraryFile = Environment.OSVersion.Platform switch
                {
                    PlatformID.Win32NT => RuntimeInformation.ProcessArchitecture switch
                    {
                        Architecture.X64 => IsAstcencAvx2Supported ? "astcenc-avx2-shared.dll" :
                                         IsAstcencSSE41Supported ? "astcenc-sse4.1-shared.dll" :
                                         IsAstcencSSE2Supported ? "astcenc-sse2-shared.dll" :
                                         throw new PlatformNotSupportedException("The required CPU instructions for x64 architecture are not supported."),
                        Architecture.Arm64 => IsAstcencSve256Supported ? "astcenc-arm-sve256-shared.dll" :
                                          IsAstcencSve128Supported ? "astcenc-arm-sve128-shared.dll" :
                                          IsAstcencNeonSupported ? "astcenc-arm-neon-shared.dll" :
                                          throw new PlatformNotSupportedException("The required CPU instructions for ARM architecture are not supported."),
                        _ => throw new PlatformNotSupportedException("Unsupported architecture"),
                    },
                    PlatformID.Unix => RuntimeInformation.ProcessArchitecture switch
                    {
                        Architecture.X64 => IsAstcencAvx2Supported ? "libastcenc-avx2-shared.so" :
                                         IsAstcencSSE41Supported ? "libastcenc-sse4.1-shared.so" :
                                         IsAstcencSSE2Supported ? "libastcenc-sse2-shared.so" :
                                         throw new PlatformNotSupportedException("The required CPU instructions for x64 architecture are not supported."),
                        Architecture.Arm64 => IsAstcencSve256Supported ? "libastcenc-sve256-shared.so" :
                                          IsAstcencSve128Supported ? "libastcenc-sve128-shared.so" :
                                          IsAstcencNeonSupported ? "libastcenc-neon-shared.so" :
                                          throw new PlatformNotSupportedException("The required CPU instructions for ARM architecture are not supported."),
                        _ => throw new PlatformNotSupportedException("Unsupported architecture"),
                    },
                    PlatformID.MacOSX => "libastcenc-shared.dylib",
                    _ => throw new PlatformNotSupportedException("Unsupported platform"),
                };

                string platformFolder = Environment.OSVersion.Platform switch
                {
                    PlatformID.Win32NT => "windows",
                    PlatformID.Unix => "linux",
                    PlatformID.MacOSX => "osx",
                    _ => throw new PlatformNotSupportedException("Unsupported platform"),
                };

                string archFolder = RuntimeInformation.ProcessArchitecture switch
                {
                    Architecture.X64 => "x64",
                    Architecture.Arm64 => "arm64",
                    _ => string.Empty,
                };

                return Path.Combine("runtimes", platformFolder, archFolder, libraryFile);
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


        public unsafe static AstcencError AstcencConfigInit(AstcencProfile profile, uint blockX, uint blockY, uint blockZ, float quality, uint flags, out AstcencConfig config)
        { 
            AstcencConfig configRef;

            AstcencError status = AstcencUnmanaged.AstcencConfigInit(profile, blockX, blockY, blockZ, quality, flags, &configRef);

            config = Unsafe.AsRef<AstcencConfig>(ref configRef);

            return status;
        }

        public unsafe static AstcencError AstcencContextAlloc(ref AstcencConfig config, uint threadCount, out AstcencContext context) 
        {
            AstcencContextInternal* astcencContext;

            AstcencError status = AstcencUnmanaged.AstcencContextAlloc((AstcencConfig*)Unsafe.AsPointer(ref config), threadCount, &astcencContext);

            context.internal_context = (IntPtr)astcencContext;

            return status;
        }

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

        public unsafe static AstcencError AstcencCompressReset(AstcencContext context) =>
            AstcencUnmanaged.AstcencCompressReset((AstcencContextInternal*)context.internal_context);

        public unsafe static AstcencError AstcencCompressCancel(AstcencContext context)=>
            AstcencUnmanaged.AstcencCompressCancel((AstcencContextInternal*)context.internal_context);

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

        public unsafe static AstcencError AstcencDecompressReset(AstcencContext context) =>
            AstcencUnmanaged.AstcencDecompressReset((AstcencContextInternal*)context.internal_context);

        public unsafe static void AstcencContextFree(AstcencContext context) =>
            AstcencUnmanaged.AstcencContextFree((AstcencContextInternal*)context.internal_context);

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

        public unsafe static string? GetErrorString(AstcencError status) 
        {
            return Marshal.PtrToStringAnsi((IntPtr)(void*)AstcencUnmanaged.AstcencGetErrorString(status));
        }
    }
}
