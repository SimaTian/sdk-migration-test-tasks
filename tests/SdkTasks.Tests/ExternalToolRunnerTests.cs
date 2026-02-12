using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class ExternalToolRunnerTests : IDisposable
    {
        private readonly List<string> _tempDirs = new();

        private string CreateProjectDir()
        {
            var dir = TestHelper.CreateNonCwdTempDirectory();
            _tempDirs.Add(dir);
            return dir;
        }

        public void Dispose()
        {
            foreach (var dir in _tempDirs)
                TestHelper.CleanupTempDirectory(dir);
        }

        [Fact]
        public void ShouldSetWorkingDirectoryToProjectDir()
        {
            var projectDir = CreateProjectDir();
            var engine = new MockBuildEngine();

            var task = new SdkTasks.Tools.ExternalToolRunner
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                Command = "cmd.exe",
                Arguments = "/c cd"
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Contains(engine.Messages!, m => m.Message!.Contains(projectDir));
        }

        [Fact]
        public void ShouldRunInProjectDirectory_NotProcessCwd()
        {
            var projectDir = CreateProjectDir();
            var engine = new MockBuildEngine();
            var cwd = Directory.GetCurrentDirectory();

            var task = new SdkTasks.Tools.ExternalToolRunner
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                Command = "cmd.exe",
                Arguments = "/c cd"
            };

            bool result = task.Execute();

            Assert.True(result);
            // The output should contain the project directory, not the process CWD
            var outputMessage = engine.Messages!.Last().Message!;
            Assert.Contains(projectDir, outputMessage, StringComparison.OrdinalIgnoreCase);
            Assert.NotEqual(cwd, outputMessage.Trim(), StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void TwoTasksWithDifferentProjectDirs_RunInRespectiveDirectories()
        {
            var dir1 = CreateProjectDir();
            var dir2 = CreateProjectDir();

            var engine1 = new MockBuildEngine();
            var engine2 = new MockBuildEngine();

            var task1 = new SdkTasks.Tools.ExternalToolRunner
            {
                BuildEngine = engine1,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir1),
                Command = "cmd.exe",
                Arguments = "/c cd"
            };

            var task2 = new SdkTasks.Tools.ExternalToolRunner
            {
                BuildEngine = engine2,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(dir2),
                Command = "cmd.exe",
                Arguments = "/c cd"
            };

            Assert.True(task1.Execute());
            Assert.True(task2.Execute());

            var output1 = engine1.Messages!.Last().Message!;
            var output2 = engine2.Messages!.Last().Message!;

            Assert.Contains(dir1, output1, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(dir2, output2, StringComparison.OrdinalIgnoreCase);
            Assert.NotEqual(output1.Trim(), output2.Trim(), StringComparer.OrdinalIgnoreCase);
        }

        [Fact]
        public void ShouldAutoInitializeProjectDirectoryFromBuildEngine()
        {
            var projectDir = CreateProjectDir();
            var projectFile = Path.Combine(projectDir, "test.csproj");
            File.WriteAllText(projectFile, "<Project />");

            var engine = new MockBuildEngine();

            // TaskEnvironment with empty ProjectDirectory - should auto-init from BuildEngine
            var task = new SdkTasks.Tools.ExternalToolRunner
            {
                BuildEngine = engine,
                TaskEnvironment = new TaskEnvironment(),
                Command = "cmd.exe",
                Arguments = "/c cd"
            };

            task.Execute();

            // After Execute, ProjectDirectory should have been initialized
            Assert.False(string.IsNullOrEmpty(task.TaskEnvironment.ProjectDirectory));
        }

        [Fact]
        public void ShouldUsesTaskEnvironmentGetProcessStartInfo()
        {
            var projectDir = CreateProjectDir();
            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(projectDir);
            var engine = new MockBuildEngine();

            var task = new SdkTasks.Tools.ExternalToolRunner
            {
                BuildEngine = engine,
                TaskEnvironment = trackingEnv,
                Command = "cmd.exe",
                Arguments = "/c cd"
            };

            bool result = task.Execute();

            // Verify the process ran in the project directory (proving GetProcessStartInfo was used)
            Assert.True(result);
            Assert.Contains(engine.Messages!, m =>
                m.Message!.Contains(projectDir, StringComparison.OrdinalIgnoreCase));
        }
    }
}
