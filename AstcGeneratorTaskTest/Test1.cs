using Astc_Encoder_CSharp_Generator;
using Moq;

namespace AstcGeneratorTaskTest
{
    [TestClass]
    public sealed class Test1
    {
        [AssemblyInitialize]
        public static void AssemblyInit(TestContext context)
        {

        }

        [AssemblyCleanup]
        public static void AssemblyCleanup()
        {
            // This method is called once for the test assembly, after all tests are run.
        }

        [TestMethod]
        public void TestMethod1()
        {
            bool success = Program.Generate("latest", "Source/astcenc.h", Path.Combine(Path.GetTempPath(), "AstcCache", "cache.json"), "ASTCENC_PUBLIC",
                                            "Astc_Encoder_CSharp", "", "AstcencImage;AstcencContext", "", true);

            //Assert
            Assert.IsTrue(success); // The execution was success
        }
    }
}
