using System;
using FluentAssertions;
using Microsoft.Build.Framework;
using SdkTasks.Diagnostics;
using SdkTasks.Tests.Infrastructure;
using Xunit;

namespace SdkTasks.Tests
{
    public class GivenAUserInputPromptMultiThreading
    {
        private readonly MockBuildEngine _buildEngine = new();
        private readonly TaskEnvironment _taskEnvironment = new();
        private readonly UserInputPrompt _task = new();

        public GivenAUserInputPromptMultiThreading()
        {
            _task.BuildEngine = _buildEngine;
            _task.TaskEnvironment = _taskEnvironment;
        }

        [Fact]
        public void ItIsDecoratedWithMSBuildMultiThreadableTaskAttribute()
        {
            typeof(UserInputPrompt).Should().BeDecoratedWith<MSBuildMultiThreadableTaskAttribute>();
        }

        [Fact]
        public void ItImplementsIMultiThreadableTask()
        {
            typeof(UserInputPrompt).Should().BeAssignableTo<IMultiThreadableTask>();
        }

        [Fact]
        public void ItLogsWarningAndReturnsEmptyString()
        {
            // Act
            bool result = _task.Execute();

            // Assert
            result.Should().BeTrue();
            _task.UserInput.Should().BeEmpty();
            _buildEngine.Warnings.Should().ContainSingle(w => w.Message.Contains("Interactive console input is not supported"));
        }
    }
}
