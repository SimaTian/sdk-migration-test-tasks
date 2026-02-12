// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using SdkTasks.Analysis;
using SdkTasks.Tests.Infrastructure;
using Xunit;

namespace SdkTasks.Tests
{
    public class ProjectReferenceAnalyzerTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly MockBuildEngine _engine;

        public ProjectReferenceAnalyzerTests()
        {
            _tempDir = TestHelper.CreateNonCwdTempDirectory();
            _engine = new MockBuildEngine();
        }

        public void Dispose() => TestHelper.CleanupTempDirectory(_tempDir);

        [Fact]
        public void Execute_TransitiveReferences_ResolveToProjectDirectory()
        {
            // Create App.csproj referencing Lib.csproj (sibling directory)
            string projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <ProjectReference Include=""..\Lib\Lib.csproj"" />
  </ItemGroup>
</Project>";

            string srcDir = Path.Combine(_tempDir, "src");
            Directory.CreateDirectory(srcDir);
            File.WriteAllText(Path.Combine(srcDir, "App.csproj"), projectContent);

            // Create Lib.csproj referencing Common.csproj (transitive)
            string libProjContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <ProjectReference Include=""..\Common\Common.csproj"" />
  </ItemGroup>
</Project>";
            string libDir = Path.Combine(_tempDir, "Lib");
            Directory.CreateDirectory(libDir);
            File.WriteAllText(Path.Combine(libDir, "Lib.csproj"), libProjContent);

            // Use the src directory as ProjectDirectory so "..\Lib\Lib.csproj" resolves correctly
            var taskEnv = SharedTestHelpers.CreateTrackingEnvironment(srcDir);
            var task = new ProjectReferenceAnalyzer
            {
                BuildEngine = _engine,
                TaskEnvironment = taskEnv,
                ProjectFilePath = "App.csproj",
                ResolveTransitive = true
            };

            bool result = task.Execute();

            Assert.True(result,
                $"Execute failed. Errors: {string.Join("; ", _engine.Errors.Select(e => e.Message))}");
            Assert.NotEmpty(task.AnalyzedReferences);

            var transitiveRef = task.AnalyzedReferences.FirstOrDefault(
                r => r.GetMetadata("IsTransitive") == "True");
            Assert.NotNull(transitiveRef);

            // The resolved reference path should be under the project dir tree, not CWD
            var refPath = transitiveRef!.GetMetadata("ReferencePath");
            if (!string.IsNullOrEmpty(refPath))
                Assert.Contains(_tempDir, refPath, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Execute_UsesGetAbsolutePath_ForProjectFileResolution()
        {
            string projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>library</OutputType>
  </PropertyGroup>
</Project>";

            File.WriteAllText(Path.Combine(_tempDir, "Simple.csproj"), projectContent);

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_tempDir);
            var task = new ProjectReferenceAnalyzer
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                ProjectFilePath = "Simple.csproj"
            };

            bool result = task.Execute();

            Assert.True(result,
                $"Execute failed. Errors: {string.Join("; ", _engine.Errors.Select(e => e.Message))}");

            // TaskEnvironment.GetAbsolutePath must be called for the project file
            SharedTestHelpers.AssertUsesGetAbsolutePath(trackingEnv);
            Assert.Contains(trackingEnv.GetAbsolutePathArgs,
                arg => arg.Contains("Simple.csproj", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Execute_UsesGetCanonicalForm_ForAdditionalSearchPaths()
        {
            string projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>library</OutputType>
  </PropertyGroup>
</Project>";

            File.WriteAllText(Path.Combine(_tempDir, "Lib.csproj"), projectContent);

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(_tempDir);
            var task = new ProjectReferenceAnalyzer
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                ProjectFilePath = "Lib.csproj",
                AdditionalSearchPaths = new ITaskItem[] { new TaskItem("extra\\path") }
            };

            bool result = task.Execute();

            Assert.True(result,
                $"Execute failed. Errors: {string.Join("; ", _engine.Errors.Select(e => e.Message))}");

            // TaskEnvironment.GetCanonicalForm must be called for additional search paths
            SharedTestHelpers.AssertUsesGetCanonicalForm(trackingEnv);
            Assert.Contains(trackingEnv.GetCanonicalFormArgs,
                arg => arg.Contains("extra", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Execute_ProjectReferences_ResolveRelativeToProjectDirectory()
        {
            string projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <ProjectReference Include=""..\Lib\Lib.csproj"" />
  </ItemGroup>
</Project>";

            string srcDir = Path.Combine(_tempDir, "src");
            Directory.CreateDirectory(srcDir);
            File.WriteAllText(Path.Combine(srcDir, "App.csproj"), projectContent);

            string libDir = Path.Combine(_tempDir, "Lib");
            Directory.CreateDirectory(libDir);
            File.WriteAllText(Path.Combine(libDir, "Lib.csproj"),
                @"<Project Sdk=""Microsoft.NET.Sdk""></Project>");

            var trackingEnv = SharedTestHelpers.CreateTrackingEnvironment(srcDir);
            var task = new ProjectReferenceAnalyzer
            {
                BuildEngine = _engine,
                TaskEnvironment = trackingEnv,
                ProjectFilePath = "App.csproj",
                ResolveTransitive = false
            };

            bool result = task.Execute();

            Assert.True(result,
                $"Execute failed. Errors: {string.Join("; ", _engine.Errors.Select(e => e.Message))}");
            Assert.NotEmpty(task.AnalyzedReferences);

            // GetAbsolutePath should be called for both the project file and its references
            Assert.True(trackingEnv.GetAbsolutePathCallCount >= 2,
                "Task should call GetAbsolutePath for both the project file and its references");

            // The project reference should resolve under _tempDir, not CWD
            var projRef = task.AnalyzedReferences.FirstOrDefault(
                r => r.GetMetadata("ReferenceType") == "ProjectReference");
            Assert.NotNull(projRef);
            string resolvedPath = projRef!.GetMetadata("ReferencePath");
            SharedTestHelpers.AssertPathUnderProjectDir(_tempDir, resolvedPath);
        }

        [Fact]
        public void Execute_DetectsProjectType_Correctly()
        {
            string projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>exe</OutputType>
  </PropertyGroup>
</Project>";

            File.WriteAllText(Path.Combine(_tempDir, "Console.csproj"), projectContent);

            var task = new ProjectReferenceAnalyzer
            {
                BuildEngine = _engine,
                TaskEnvironment = SharedTestHelpers.CreateTrackingEnvironment(_tempDir),
                ProjectFilePath = "Console.csproj"
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.Equal("ConsoleApplication", task.ProjectType);
        }

        [Fact]
        public void Execute_PackageReferences_IncludedInOutput()
        {
            string projectContent = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <ItemGroup>
    <PackageReference Include=""Newtonsoft.Json"" Version=""13.0.3"" />
  </ItemGroup>
</Project>";

            File.WriteAllText(Path.Combine(_tempDir, "WithPkg.csproj"), projectContent);

            var task = new ProjectReferenceAnalyzer
            {
                BuildEngine = _engine,
                TaskEnvironment = SharedTestHelpers.CreateTrackingEnvironment(_tempDir),
                ProjectFilePath = "WithPkg.csproj"
            };

            bool result = task.Execute();

            Assert.True(result);
            Assert.NotEmpty(task.AnalyzedReferences);

            var pkgRef = task.AnalyzedReferences.FirstOrDefault(
                r => r.GetMetadata("ReferenceType") == "PackageReference");
            Assert.NotNull(pkgRef);
            Assert.Equal("Newtonsoft.Json", pkgRef!.ItemSpec);
            Assert.Equal("13.0.3", pkgRef.GetMetadata("ReferencePath"));
        }
    }
}
