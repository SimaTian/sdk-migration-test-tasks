using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;
using SdkTasks.Diagnostics;
using System;
using System.IO;

namespace SdkTasks.Tests
{
    public class UserInputPromptTests
    {
        [Fact]
        public void ShouldNotReadFromConsoleInMultithreadedMode()
        {
            // Arrange
            var engine = new MockBuildEngine();
            var task = new UserInputPrompt
            {
                BuildEngine = engine
            };
            
            // We simulate console input
            var originalIn = Console.In;
            string consoleInput = "Console input";
            try
            {
                Console.SetIn(new StringReader(consoleInput));

                // Act
                bool success = task.Execute();

                // Assert
                Assert.True(success);
                // In multithreaded mode, we expect it NOT to read from console
                Assert.NotEqual(consoleInput, task.UserInput);
                Assert.Equal(string.Empty, task.UserInput ?? string.Empty);
            }
            finally
            {
                Console.SetIn(originalIn);
            }
        }
    }
}
