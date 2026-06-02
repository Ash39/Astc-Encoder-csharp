using AstcEncoder;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using StbImageSharp;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AstcEncoderTest
{
    [TestClass]
    public sealed class Test1
    {
        private static (int height, int width, byte[] imageBytes)[]? images;
        private static (int height, int width, byte[] imageBytes)[]? volumeImages;

        [ClassInitialize]
        public static void ClassInit(TestContext context)
        {
            string[] files = Directory.GetFiles("Files");
            string[] VolumeFiles = Directory.GetFiles("Files/128x128/Cracks");

            images = LoadImages(files);
            volumeImages = LoadImages(VolumeFiles);
        }

        private static (int, int, byte[])[] LoadImages(string[] files)
        {
            var images = new (int, int, byte[])[files.Length];

            for (int i = 0; i < files.Length; i++)
            {
                using (Stream fileStream = File.Open(files[i], FileMode.Open))
                {
                    ImageResult result = ImageResult.FromStream(fileStream, ColorComponents.RedGreenBlueAlpha);

                    (int height, int width, byte[] imageBytes) imageInfo;

                    imageInfo.imageBytes = result.Data;
                    imageInfo.height = result.Height;
                    imageInfo.width = result.Width;

                    images[i] = imageInfo;
                }
            }

            return images;
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            images = null;
        }

        [TestMethod]
        public void DecodeTest()
        {
            AstcencSwizzle swizzle = new AstcencSwizzle()
            {
                r = AstcencSwz.AstcencSwzR,
                g = AstcencSwz.AstcencSwzG,
                b = AstcencSwz.AstcencSwzB,
                a = AstcencSwz.AstcencSwzA

            };

            byte[] data =
            [
                0x84,0x00,0x38,0xC8,0x00,0x00,0x00,0x00,
                0x00,0x00,0x00,0x00,0x00,0xB3,0x4D,0x78
            ];

            byte[] output = new byte[12 * 12 * 4];

            AstcencError status = Astcenc.AstcencConfigInit(AstcencProfile.AstcencPrfLdr, 12, 12, 1, Astcenc.AstcencPreMedium, 0, out AstcencConfig config);

            Assert.AreEqual(AstcencError.AstcencSuccess, status);

            AstcencContext context;

            status = Astcenc.AstcencContextAlloc(ref config, 1, out context);

            Assert.AreEqual(AstcencError.AstcencSuccess, status);

            AstcencImage outImage;
            outImage.dimX = 12;
            outImage.dimY = 12;
            outImage.dimZ = 1;
            outImage.dataType = AstcencType.AstcencTypeU8;
            outImage.data = [output];

            status = Astcenc.AstcencDecompressImage(context, data, ref outImage, swizzle, 0);

            Assert.AreEqual(AstcencError.AstcencSuccess, status);

            for (int y = 0; y < 12; y++)
            {
                for (int x = 0; x < 12; x++)
                {
                    int index = (12 * 4 * y) + (4 * x);

                    byte r = output[index + 0];
                    byte g = output[index + 1];
                    byte b = output[index + 2];
                    byte a = output[index + 3];

                    Console.WriteLine($"[{x,2}x{y,2}] = {r:000}, {g:000}, {b:000}, {a:000}");
                }
            }

            Astcenc.AstcencContextFree(context);
        }

        [TestMethod]
        public void EncodeAndDecodeTest()
        {
            (int height, int width, byte[] imageBytes) = images![0];

            const uint thread_count = 1;
            const uint block_x = 6;
            const uint block_y = 6;
            const uint block_z = 1;
            const AstcencProfile profile = AstcencProfile.AstcencPrfLdr;
            float quality = Astcenc.AstcencPreMedium;
            AstcencSwizzle swizzle = new AstcencSwizzle()
            {
                r = AstcencSwz.AstcencSwzR,
                g = AstcencSwz.AstcencSwzG,
                b = AstcencSwz.AstcencSwzB,
                a = AstcencSwz.AstcencSwzA

            };


            AstcencError status = Astcenc.AstcencConfigInit(profile, block_x, block_y, block_z, quality, 0, out AstcencConfig config);

            Assert.AreEqual(AstcencError.AstcencSuccess, status);

            AstcencContext context;

            status = Astcenc.AstcencContextAlloc(ref config, thread_count, out context);

            Assert.AreEqual(AstcencError.AstcencSuccess, status);

            AstcencImage image;
            image.dimX = (uint)width;
            image.dimY = (uint)height;
            image.dimZ = 1;
            image.dataType = AstcencType.AstcencTypeU8;
            image.data = [imageBytes];

            uint block_count_x = ((uint)width + block_x - 1) / block_x;
            uint block_count_y = ((uint)height + block_y - 1) / block_y;

            uint compLen = block_count_x * block_count_y * 16;
            byte[] comp_data = new byte[compLen];

            status = Astcenc.AstcencCompressImage(context, ref image, swizzle, comp_data, 0);

            Assert.AreEqual(AstcencError.AstcencSuccess, status);

            byte[] decompressedBytes = new byte[imageBytes!.Length];

            AstcencImage outImage;
            outImage.dimX = (uint)width;
            outImage.dimY = (uint)height;
            outImage.dimZ = 1;
            outImage.dataType = AstcencType.AstcencTypeU8;
            outImage.data = [decompressedBytes];

            status = Astcenc.AstcencDecompressImage(context, comp_data, ref outImage, swizzle, 0);

            Assert.AreEqual(AstcencError.AstcencSuccess, status);
            Assert.HasCount(imageBytes.Length, decompressedBytes);

            Astcenc.AstcencContextFree(context);
        }

        [TestMethod]
        public void EncodeAndDecodeVolumeTextureTest()
        {
            const uint thread_count = 1;
            const uint block_x = 4;
            const uint block_y = 4;
            const uint block_z = 4;
            const AstcencProfile profile = AstcencProfile.AstcencPrfLdr;
            float quality = Astcenc.AstcencPreMedium;
            AstcencSwizzle swizzle = new AstcencSwizzle()
            {
                r = AstcencSwz.AstcencSwzR,
                g = AstcencSwz.AstcencSwzG,
                b = AstcencSwz.AstcencSwzB,
                a = AstcencSwz.AstcencSwzA

            };

            AstcencError status = Astcenc.AstcencConfigInit(profile, block_x, block_y, block_z, quality, 0, out AstcencConfig config);

            Assert.AreEqual(AstcencError.AstcencSuccess, status);

            AstcencContext context;

            status = Astcenc.AstcencContextAlloc(ref config, thread_count, out context);

            Assert.AreEqual(AstcencError.AstcencSuccess, status);

            (int height, int width, byte[] imageBytes) = volumeImages![0];

            byte[][] slices = new byte[volumeImages!.Length][];

            for (int i = 0; i < volumeImages.Length; i++)
            {
                (int h, int w, byte[] bytes) = volumeImages[i];
                slices[i] = bytes;
            }

            AstcencImage image;
            image.dimX = (uint)width;
            image.dimY = (uint)height;
            image.dimZ = (uint)volumeImages!.Length;
            image.dataType = AstcencType.AstcencTypeU8;
            image.data = slices;

            uint block_count_x = ((uint)width + block_x - 1) / block_x;
            uint block_count_y = ((uint)height + block_y - 1) / block_y;
            uint block_count_z = ((uint)volumeImages!.Length + block_z - 1) / block_z;

            uint compLen = block_count_x * block_count_y * block_count_z * 16;
            byte[] comp_data = new byte[compLen];

            status = Astcenc.AstcencCompressImage(context, ref image, swizzle, comp_data, 0);

            Assert.AreEqual(AstcencError.AstcencSuccess, status);

            byte[][] decompressedBytes = new byte[volumeImages!.Length][];

            for (int i = 0; i < volumeImages!.Length; i++)
            {
                decompressedBytes[i] = new byte[imageBytes.Length];
            }

            AstcencImage outImage;
            outImage.dimX = (uint)width;
            outImage.dimY = (uint)height;
            outImage.dimZ = (uint)volumeImages!.Length;
            outImage.dataType = AstcencType.AstcencTypeU8;
            outImage.data = decompressedBytes;

            status = Astcenc.AstcencDecompressImage(context, comp_data, ref outImage, swizzle, 0);

            Assert.AreEqual(AstcencError.AstcencSuccess, status);
            Assert.HasCount(volumeImages!.Length, decompressedBytes);

            for (int i = 0; i < decompressedBytes.Length; i++)
            {
                Assert.HasCount(imageBytes.Length, decompressedBytes[i]);
            }

            Astcenc.AstcencContextFree(context);
        }

        [TestMethod]
        public void BlockInfoTest()
        {
            (int height, int width, byte[] imageBytes) = images![0];

            const uint thread_count = 1;
            const uint block_x = 6;
            const uint block_y = 6;
            const uint block_z = 1;
            const AstcencProfile profile = AstcencProfile.AstcencPrfLdr;
            float quality = Astcenc.AstcencPreMedium;
            AstcencSwizzle swizzle = new AstcencSwizzle()
            {
                r = AstcencSwz.AstcencSwzR,
                g = AstcencSwz.AstcencSwzG,
                b = AstcencSwz.AstcencSwzB,
                a = AstcencSwz.AstcencSwzA

            };


            AstcencError status = Astcenc.AstcencConfigInit(profile, block_x, block_y, block_z, quality, 0, out AstcencConfig config);

            Assert.AreEqual(AstcencError.AstcencSuccess, status);

            AstcencContext context;

            status = Astcenc.AstcencContextAlloc(ref config, thread_count, out context);

            Assert.AreEqual(AstcencError.AstcencSuccess, status);

            AstcencImage image;
            image.dimX = (uint)width;
            image.dimY = (uint)height;
            image.dimZ = 1;
            image.dataType = AstcencType.AstcencTypeU8;
            image.data = [imageBytes];

            uint block_count_x = ((uint)width + block_x - 1) / block_x;
            uint block_count_y = ((uint)height + block_y - 1) / block_y;

            uint compLen = block_count_x * block_count_y * 16;
            Span<byte> comp_data = new byte[compLen];

            status = Astcenc.AstcencCompressImage(context, ref image, swizzle, comp_data, 0);

            Assert.AreEqual(AstcencError.AstcencSuccess, status);

            int index = 0;

            for (int blockY = 0; blockY < block_count_y; blockY++)
            {
                for (int blockX = 0; blockX < block_count_x; blockX++)
                {
                    status = Astcenc.AstcencGetBlockInfo(context, comp_data.Slice(index, 16), out AstcencBlockInfo blockInfo);

                    Assert.AreEqual(AstcencError.AstcencSuccess, status);
                    index += 16;
                }
            }
            Astcenc.AstcencContextFree(context);
        }

        [TestMethod]
        public void EncodeMultithreadTest()
        {
            ConcurrentQueue<(int height, int width, byte[] imageBytes)> imageQueue = new(images!);

            int threadCount = 8;
            const uint block_x = 6;
            const uint block_y = 6;
            const uint block_z = 1;
            const AstcencProfile profile = AstcencProfile.AstcencPrfLdr;
            float quality = Astcenc.AstcencPreMedium;

            AstcencSwizzle swizzle = new AstcencSwizzle()
            {
                r = AstcencSwz.AstcencSwzR,
                g = AstcencSwz.AstcencSwzG,
                b = AstcencSwz.AstcencSwzB,
                a = AstcencSwz.AstcencSwzA

            };

            AstcencError status = Astcenc.AstcencConfigInit(
                profile, block_x, block_y, block_z, quality, 0, out AstcencConfig config);
            Assert.AreEqual(AstcencError.AstcencSuccess, status);

            while (imageQueue.Count > 0)
            {
                if (imageQueue.TryDequeue(out (int height, int width, byte[] imageBytes) imageInfo))
                {
                    (int height, int width, byte[] imageBytes) = imageInfo;

                    status = Astcenc.AstcencContextAlloc(ref config, (uint)threadCount, out AstcencContext context);
                    Assert.AreEqual(AstcencError.AstcencSuccess, status);

                    uint blockCountX = ((uint)width + block_x - 1) / block_x;
                    uint blockCountY = ((uint)height + block_y - 1) / block_y;
                    uint compLen = blockCountX * blockCountY * 16;

                    byte[] compData = new byte[compLen];

                    Astcenc.AstcencCompressReset(context);

                    Task[] tasks = new Task[threadCount];

                    for (int i = 0; i < threadCount; i++)
                    {
                        int threadIndex = i; 

                        tasks[i] = Task.Run(() =>
                        {
                            AstcencImage image = new AstcencImage
                            {
                                dimX = (uint)width,
                                dimY = (uint)height,
                                dimZ = 1,
                                dataType = AstcencType.AstcencTypeU8,
                                data = [imageBytes]
                            };

                            AstcencError err = Astcenc.AstcencCompressImage(
                                context,
                                ref image,
                                swizzle,
                                compData,
                                (uint)threadIndex);

                            if (err != AstcencError.AstcencSuccess)
                                throw new InvalidOperationException($"Thread {threadIndex} failed: {err}");
                        });
                    }

                    // Wait for all workers
                    Task.WaitAll(tasks);

                    // Safe to free after all tasks complete
                    Astcenc.AstcencContextFree(context);
                }
            }
        }
    }
}
