using System;
using System.IO;
using Xunit;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Tests.Infrastructure;
using SdkTasks.Build;

namespace SdkTasks.Tests
{
    public class AssemblyCacheResolverMultiThreading : IDisposable
    {
        private readonly string _projectDir1;
        private readonly string _projectDir2;
        private readonly MockBuildEngine _engine;

        public AssemblyCacheResolverMultiThreading()
        {
            _projectDir1 = TestHelper.CreateNonCwdTempDirectory();
            _projectDir2 = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose()
        {
            TestHelper.CleanupTempDirectory(_projectDir1);
            TestHelper.CleanupTempDirectory(_projectDir2);
        }

        [Fact]
        public void StaticCache_ShouldNotLeakBetweenProjects()
        {
            // Setup Project 1: has MyLib.dll in bin/
            string bin1 = Path.Combine(_projectDir1, "bin");
            Directory.CreateDirectory(bin1);
            string dll1 = Path.Combine(bin1, "MyLib.dll");
            File.WriteAllText(dll1, "dll1");

            // Setup Project 2: has MyLib.dll in lib/net8.0/
            string lib2 = Path.Combine(_projectDir2, "lib", "net8.0");
            Directory.CreateDirectory(lib2);
            string dll2 = Path.Combine(lib2, "MyLib.dll");
            File.WriteAllText(dll2, "dll2");

            // Run Task 1
            var task1 = new AssemblyCacheResolver
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir1),
                AssemblyReferences = new ITaskItem[] { new TaskItem("MyLib") }
            };
            Assert.True(task1.Execute());
            Assert.Single(task1.ResolvedReferences);
            Assert.Equal(dll1, task1.ResolvedReferences[0].ItemSpec);

            // Run Task 2
            var task2 = new AssemblyCacheResolver
            {
                BuildEngine = _engine,
                TaskEnvironment = TaskEnvironmentHelper.CreateForTest(_projectDir2),
                AssemblyReferences = new ITaskItem[] { new TaskItem("MyLib") }
            };
            Assert.True(task2.Execute());
            Assert.Single(task2.ResolvedReferences);
            
            // This checks that we didn't get the path from Project 1
            Assert.Equal(dll2, task2.ResolvedReferences[0].ItemSpec);
        }

        [Fact]
        public void TaskEnvironment_ShouldInitializeFromBuildEngine_WhenNull()
        {
            var engine = new MockBuildEngine();
            string projectFile = Path.Combine(_projectDir1, "test.proj");
            File.WriteAllText(projectFile, "<Project />");
            engine.ProjectFileOfTaskNode = projectFile;

            var task = new AssemblyCacheResolver
            {
                BuildEngine = engine,
                TaskEnvironment = null!, // Intentionally null
                AssemblyReferences = new ITaskItem[] { }
            };

            bool success = task.Execute();
            Assert.True(success);
            
            // Should be initialized
            Assert.NotNull(task.TaskEnvironment);
            Assert.Equal(_projectDir1, task.TaskEnvironment.ProjectDirectory);
        }

        [Fact]
        public void Execution_ShouldBeConsistent_BetweenAutoInitAndExplicitInit()
        {
            // Setup common environment
            string projectDir = _projectDir1;
            string binDir = Path.Combine(projectDir, "bin");
            Directory.CreateDirectory(binDir);
            string dllPath = Path.Combine(binDir, "MyLib.dll");
            File.WriteAllText(dllPath, "dummy content");
            
            string projectFile = Path.Combine(projectDir, "test.proj");
            File.WriteAllText(projectFile, "<Project/>");

            var assemblyRef = new TaskItem("MyLib");
            var references = new ITaskItem[] { assemblyRef };

            // Run 1: Auto-init (Simulates legacy invocation where ProjectFileOfTaskNode drives the path)
            var savedCwd = Environment.CurrentDirectory;
            ITaskItem[]? results1 = null;
            try 
            {
                Environment.CurrentDirectory = projectDir;
                var engine1 = new MockBuildEngine { ProjectFileOfTaskNode = projectFile };
                var task1 = new AssemblyCacheResolver
                {
                    BuildEngine = engine1,
                    TaskEnvironment = null!, // Trigger auto-init
                    AssemblyReferences = references
                };
                
                Assert.True(task1.Execute());
                results1 = task1.ResolvedReferences;
            }
            finally
            {
                Environment.CurrentDirectory = savedCwd;
            }

            // Run 2: Explicit Init (Multi-threaded usage, CWD is irrelevant)
            ITaskItem[]? results2 = null;
            try
            {
                // Set CWD to something unrelated to ensure we don't accidentally rely on it
                Environment.CurrentDirectory = _projectDir2; 
                var engine2 = new MockBuildEngine { ProjectFileOfTaskNode = projectFile };
                var task2 = new AssemblyCacheResolver
                {
                    BuildEngine = engine2,
                    TaskEnvironment = new TaskEnvironment { ProjectDirectory = projectDir }, // Explicit env
                    AssemblyReferences = references
                };

                Assert.True(task2.Execute());
                results2 = task2.ResolvedReferences;
            }
            finally
            {
                Environment.CurrentDirectory = savedCwd;
            }

            // Assert Consistency
            Assert.NotNull(results1);
            Assert.NotNull(results2);
            Assert.Single(results1);
            Assert.Single(results2);
            Assert.Equal(results1[0].ItemSpec, results2[0].ItemSpec);
            Assert.Equal(results1[0].GetMetadata("ResolvedFrom"), results2[0].GetMetadata("ResolvedFrom"));
        }
    }
}
