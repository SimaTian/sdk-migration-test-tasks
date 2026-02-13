using System;
using System.IO;
using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class ProcessTerminatorTests
    {
        [Fact]
        public void ShouldReturnFalseAndLogError()
        {
            var engine = new MockBuildEngine();
            var task = new SdkTasks.Tools.ProcessTerminator
            {
                BuildEngine = engine
            };

            bool result = task.Execute();

            Assert.False(result);
            Assert.Single(engine.Errors);
        }

        [Fact]
        public void ShouldInitializeProjectDirectoryFromBuildEngine()
        {
            string projectDir = TestHelper.CreateNonCwdTempDirectory();
            try
            {
                string projectFile = Path.Combine(projectDir, "test.csproj");
                File.WriteAllText(projectFile, "<Project />");

                var engine = new MockBuildEngine { ProjectFileOfTaskNode = projectFile };
                var task = new SdkTasks.Tools.ProcessTerminator
                {
                    BuildEngine = engine,
                    TaskEnvironment = new TaskEnvironment()
                };

                task.Execute();

                string expectedDir = Path.GetDirectoryName(Path.GetFullPath(projectFile))!;
                Assert.Equal(expectedDir, task.TaskEnvironment.ProjectDirectory);
            }
            finally
            {
                TestHelper.CleanupTempDirectory(projectDir);
            }
        }
    }
}
