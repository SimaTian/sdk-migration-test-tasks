using Xunit;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Tests.Infrastructure;
using SdkTasks.Diagnostics;

namespace SdkTasks.Tests
{
    public class DiagnosticReportAggregatorTests : IDisposable
    {
        private readonly List<string> _tempDirs = new();

        private string CreateTempDir()
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
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(DiagnosticReportAggregator),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldImplementIMultiThreadableTask()
        {
            var task = new DiagnosticReportAggregator();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void ShouldHaveTaskEnvironmentProperty()
        {
            var task = new DiagnosticReportAggregator();
            Assert.NotNull(task.TaskEnvironment);
        }

        [Fact]
        public void ShouldResolveRelativePathsViaTaskEnvironment()
        {
            var projectDir = CreateTempDir();
            var logsDir = Path.Combine(projectDir, "logs");
            Directory.CreateDirectory(logsDir);

            // Create a log file with a diagnostic entry
            File.WriteAllText(Path.Combine(logsDir, "build.log"),
                "src\\Program.cs(10,5): error CS1002: ; expected\n");

            var reportPath = Path.Combine(projectDir, "report.html");

            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = projectDir };
            var engine = new MockBuildEngine();
            var task = new DiagnosticReportAggregator
            {
                BuildEngine = engine,
                TaskEnvironment = trackingEnv,
                SourceDirectories = new ITaskItem[] { new TaskItem("logs") },
                ReportOutputPath = "report.html"
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.True(trackingEnv.GetAbsolutePathCallCount > 0,
                "Task should use TaskEnvironment.GetAbsolutePath for path resolution");
            Assert.True(File.Exists(reportPath), "Report should be created under ProjectDirectory");
            Assert.Equal(1, task.ErrorCount);
        }

        [Fact]
        public void ShouldUseTaskEnvironmentForEnvironmentVariables()
        {
            var projectDir = CreateTempDir();
            var logsDir = Path.Combine(projectDir, "logs");
            Directory.CreateDirectory(logsDir);
            File.WriteAllText(Path.Combine(logsDir, "build.log"), "no diagnostics here\n");

            var trackingEnv = new TrackingTaskEnvironment { ProjectDirectory = projectDir };
            trackingEnv.SetEnvironmentVariable("BUILD_NUMBER", "42");
            var engine = new MockBuildEngine();
            var task = new DiagnosticReportAggregator
            {
                BuildEngine = engine,
                TaskEnvironment = trackingEnv,
                SourceDirectories = new ITaskItem[] { new TaskItem("logs") },
                ReportOutputPath = "report.html"
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.True(trackingEnv.GetEnvironmentVariableCallCount > 0,
                "Task should use TaskEnvironment.GetEnvironmentVariable instead of Environment.GetEnvironmentVariable");
            string reportContent = File.ReadAllText(Path.Combine(projectDir, "report.html"));
            Assert.Contains("42", reportContent);
        }

        [Fact]
        public void ShouldCountErrorsAndWarnings()
        {
            var projectDir = CreateTempDir();
            var logsDir = Path.Combine(projectDir, "logs");
            Directory.CreateDirectory(logsDir);

            File.WriteAllText(Path.Combine(logsDir, "build.log"),
                "src\\A.cs(1,1): error CS1002: ; expected\n" +
                "src\\B.cs(2,1): warning CS0168: Variable declared but never used\n" +
                "src\\C.cs(3,1): error MSB3021: Unable to copy file\n");

            var engine = new MockBuildEngine();
            var task = new DiagnosticReportAggregator
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                SourceDirectories = new ITaskItem[] { new TaskItem("logs") },
                ReportOutputPath = "report.html"
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal(2, task.ErrorCount);
            Assert.Equal(1, task.WarningCount);
        }

        [Fact]
        public void ShouldExcludeWarningsWhenDisabled()
        {
            var projectDir = CreateTempDir();
            var logsDir = Path.Combine(projectDir, "logs");
            Directory.CreateDirectory(logsDir);

            File.WriteAllText(Path.Combine(logsDir, "build.log"),
                "src\\A.cs(1,1): error CS1002: ; expected\n" +
                "src\\B.cs(2,1): warning CS0168: Variable declared but never used\n");

            var engine = new MockBuildEngine();
            var task = new DiagnosticReportAggregator
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                SourceDirectories = new ITaskItem[] { new TaskItem("logs") },
                ReportOutputPath = "report.html",
                IncludeWarnings = false
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal(1, task.ErrorCount);
            Assert.Equal(0, task.WarningCount);
        }

        [Fact]
        public void ShouldGenerateHtmlReport()
        {
            var projectDir = CreateTempDir();
            var logsDir = Path.Combine(projectDir, "logs");
            Directory.CreateDirectory(logsDir);
            File.WriteAllText(Path.Combine(logsDir, "build.log"),
                "src\\A.cs(1,1): error CS1002: ; expected\n");

            var engine = new MockBuildEngine();
            var task = new DiagnosticReportAggregator
            {
                BuildEngine = engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(projectDir),
                SourceDirectories = new ITaskItem[] { new TaskItem("logs") },
                ReportOutputPath = "report.html"
            };

            bool result = task.Execute();

            Assert.True(result);
            var reportPath = Path.Combine(projectDir, "report.html");
            Assert.True(File.Exists(reportPath));
            string content = File.ReadAllText(reportPath);
            Assert.Contains("<!DOCTYPE html>", content);
            Assert.Contains("</html>", content);
            Assert.Contains("CS1002", content);
        }
    }
}
