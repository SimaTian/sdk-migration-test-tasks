using Xunit;
using Microsoft.Build.Framework;
using SdkTasks.Tests.Infrastructure;

namespace SdkTasks.Tests
{
    public class ProcessTerminatorTests
    {
        [Fact]
        public void HasMSBuildMultiThreadableTaskAttribute()
        {
            var attr = Attribute.GetCustomAttribute(
                typeof(SdkTasks.Tools.ProcessTerminator),
                typeof(MSBuildMultiThreadableTaskAttribute));
            Assert.NotNull(attr);
        }

        [Fact]
        public void ShouldImplementIMultiThreadableTask()
        {
            var task = new SdkTasks.Tools.ProcessTerminator();
            Assert.IsAssignableFrom<IMultiThreadableTask>(task);
        }

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
    }
}
