using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Compilation;
using SdkTasks.Tests.Infrastructure;
using Xunit;

namespace SdkTasks.Tests
{
    public class GivenBatchItemProcessorMultiThreading
    {
        [Fact]
        public void ItHasMultiThreadableAttribute()
        {
            var attributes = typeof(BatchItemProcessor).GetCustomAttributes(typeof(MSBuildMultiThreadableTaskAttribute), false);
            Assert.Single(attributes);
        }

        [Fact]
        public void ItImplementsIMultiThreadableTask()
        {
            Assert.True(typeof(IMultiThreadableTask).IsAssignableFrom(typeof(BatchItemProcessor)));
        }

        [Fact]
        public void ItInitializesProjectDirectoryFromBuildEngineWhenMissing()
        {
            // Arrange
            var task = new BatchItemProcessor();
            
            // Set up TaskEnvironment with empty ProjectDirectory (default)
            task.TaskEnvironment = new TaskEnvironment();
            
            // Set up BuildEngine with a project file path
            // We use an absolute path for the project file
            string projectDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            string projectFile = Path.Combine(projectDir, "test.csproj");
            
            var buildEngine = new MockBuildEngine();
            // MockBuildEngine in this repo might need specific property setting or method mocking
            // Assuming standard mock or I'll check MockBuildEngine.cs content next
            buildEngine.ProjectFileOfTaskNode = projectFile;
            task.BuildEngine = buildEngine;

            task.RelativePaths = new[] { "input.txt" };

            // Act
            bool success = task.Execute();

            // Assert
            Assert.True(success);
            Assert.Single(task.AbsolutePaths);
            
            string expectedPath = Path.Combine(projectDir, "input.txt");
            
            // If defensive initialization is missing, it will return just "input.txt" (relative)
            // or resolve against CWD if using Path.GetFullPath (but here it uses Path.Combine with empty string)
            
            // We expect it to match the project dir
            Assert.Equal(expectedPath, task.AbsolutePaths[0]);
        }
    }
}
