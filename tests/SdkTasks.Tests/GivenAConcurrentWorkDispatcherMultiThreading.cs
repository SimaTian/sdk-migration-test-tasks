using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Build;
using SdkTasks.Tests.Infrastructure;
using Xunit;

namespace SdkTasks.Tests
{
    public class GivenAConcurrentWorkDispatcherMultiThreading
    {
        [Fact]
        public void ItImplementsIMultiThreadableTask()
        {
            var task = new ConcurrentWorkDispatcher();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

        [Fact]
        public void ItHasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = typeof(ConcurrentWorkDispatcher).GetCustomAttribute<MSBuildMultiThreadableTaskAttribute>();
            Assert.NotNull(attr);
        }

        [Fact]
        public void ItResolvesRelativePathsAgainstProjectFileLocationWhenTaskEnvironmentNotSet()
        {
            // Setup
            var tempRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            var projectDir = Path.Combine(tempRoot, "Project");
            var workDir = Path.Combine(tempRoot, "Work"); // This will be CWD
            
            Directory.CreateDirectory(projectDir);
            Directory.CreateDirectory(workDir);

            try
            {
                // Create a file in projectDir
                var fileName = "config.xml";
                var projectFilePath = Path.Combine(projectDir, "test.csproj");
                var expectedFile = Path.Combine(projectDir, fileName);
                File.WriteAllText(expectedFile, "<config/>");

                // Switch CWD to workDir
                var originalCwd = Environment.CurrentDirectory;
                Environment.CurrentDirectory = workDir;

                try
                {
                    var task = new ConcurrentWorkDispatcher();
                    task.BuildEngine = new MockBuildEngine 
                    { 
                        ProjectFileOfTaskNode = projectFilePath 
                    };
                    // Explicitly NOT setting TaskEnvironment.ProjectDirectory
                    // task.TaskEnvironment is initialized to default (empty ProjectDirectory) by the task constructor

                    var item = new TaskItem(fileName);
                    item.SetMetadata("Category", "ConfigPath"); 
                    item.SetMetadata("ConfigPath", fileName);
                    
                    task.WorkItems = new[] { item };

                    // Execute
                    var result = task.Execute();

                    // Verify
                    Assert.True(result);
                    Assert.Single(task.CompletedItems);
                    
                    var outputItem = task.CompletedItems[0];
                    var resolvedPath = outputItem.GetMetadata("ResolvedPath");
                    var exists = outputItem.GetMetadata("Exists");

                    // The resolved path should be absolute and point to the file in projectDir
                    Assert.Equal(expectedFile, resolvedPath);
                    Assert.Equal("True", exists);
                }
                finally
                {
                    Environment.CurrentDirectory = originalCwd;
                }
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, true);
            }
        }
    }
}
