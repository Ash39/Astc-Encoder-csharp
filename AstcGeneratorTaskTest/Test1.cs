using Astc_Encoder_CSharp_Generator;
using Microsoft.Build.Framework;
using Moq;

namespace AstcGeneratorTaskTest
{
    [TestClass]
    public sealed class Test1
    {
        private static Mock<IBuildEngine> buildEngine;
        private static List<BuildErrorEventArgs> errors;
        private static AstcGeneratorTask generator;

        [AssemblyInitialize]
        public static void AssemblyInit(TestContext context)
        {
            buildEngine = new Mock<IBuildEngine>();
            errors = new List<BuildErrorEventArgs>();
            buildEngine.Setup(x => x.LogErrorEvent(It.IsAny<BuildErrorEventArgs>())).Callback<BuildErrorEventArgs>(e => errors.Add(e));

            var item = new Mock<ITaskItem>();
            item.Setup(x => x.GetMetadata("Identity")).Returns($".\\Resources\\complete-prop.setting");

            generator = new AstcGeneratorTask();
            generator.ASTCVersion = "latest";
            generator.ASTCSourceFilePath = "Source/astcenc.h";
            generator.CacheFile = Path.Combine(Path.GetTempPath(), "AstcCache", "cache.json");
            generator.MethodExportName = "ASTCENC_PUBLIC";
            generator.ProjectPath = "";
            generator.GeneraedFilesNamespace = "Astc_Encoder_CSharp";
            generator.InternalTypes = "AstcencImage;AstcencContext";
            generator.DisableXmlCache = true;
            generator.BuildEngine = buildEngine.Object;
        }

        [AssemblyCleanup]
        public static void AssemblyCleanup()
        {
            // This method is called once for the test assembly, after all tests are run.
        }

        [TestMethod]
        public void TestMethod1()
        {
            bool success = generator.Execute();

            //Assert
            Assert.IsTrue(success); // The execution was success
            Assert.IsEmpty(errors); //Not error were found
        }
    }
}
