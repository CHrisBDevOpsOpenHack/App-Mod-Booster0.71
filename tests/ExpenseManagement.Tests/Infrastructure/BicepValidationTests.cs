using FluentAssertions;
using System.Diagnostics;
using Xunit;

namespace ExpenseManagement.Tests.Infrastructure;

/// <summary>
/// Tests for Bicep template validation
/// Validates infrastructure-as-code without deploying
/// </summary>
public class BicepValidationTests
{
    private readonly string _repoRoot;

    public BicepValidationTests()
    {
        // Find repository root (look for .git directory)
        _repoRoot = FindRepoRoot() ?? Directory.GetCurrentDirectory();
    }

    private string? FindRepoRoot()
    {
        var current = Directory.GetCurrentDirectory();
        
        while (!string.IsNullOrEmpty(current))
        {
            if (Directory.Exists(Path.Combine(current, ".git")) ||
                File.Exists(Path.Combine(current, "deploy-all.ps1")))
            {
                return current;
            }
            
            var parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }
        
        return null;
    }

    [Fact]
    public void BicepFiles_Exist()
    {
        // Arrange
        var infraDir = Path.Combine(_repoRoot, "deploy-infra");
        var mainBicep = Path.Combine(infraDir, "main.bicep");

        // Assert
        Directory.Exists(infraDir).Should().BeTrue("deploy-infra directory should exist");
        File.Exists(mainBicep).Should().BeTrue("main.bicep should exist");
    }

    [Fact]
    public void AllBicepFiles_CompileWithoutErrors()
    {
        // Skip if az CLI is not available
        if (!IsAzCliAvailable())
        {
            return;
        }

        // Arrange
        var infraDir = Path.Combine(_repoRoot, "deploy-infra");
        var bicepFiles = Directory.GetFiles(infraDir, "*.bicep", SearchOption.AllDirectories);

        bicepFiles.Should().NotBeEmpty("should find Bicep files");

        foreach (var bicepFile in bicepFiles)
        {
            // Act
            var result = RunCommand("az", $"bicep build --file \"{bicepFile}\"");

            // Assert
            result.ExitCode.Should().Be(0, 
                $"Bicep file {Path.GetFileName(bicepFile)} should compile without errors. Output: {result.Error}");
        }
    }

    [Fact]
    public void MainBicep_HasRequiredOutputs()
    {
        // Arrange
        var mainBicep = Path.Combine(_repoRoot, "deploy-infra", "main.bicep");
        
        if (!File.Exists(mainBicep))
        {
            // Skip if file doesn't exist
            return;
        }

        var content = File.ReadAllText(mainBicep);

        // Assert - Check for required outputs
        var requiredOutputs = new[]
        {
            "webAppName",           // The name of the App Service
            "sqlServerFqdn",        // SQL Server FQDN
            "databaseName",         // Database name
            "managedIdentityClientId" // Managed Identity Client ID
        };

        foreach (var output in requiredOutputs)
        {
            content.Should().Contain($"output {output}", 
                $"main.bicep should define output '{output}'");
        }
    }

    [Fact]
    public void BicepFiles_DoNotUseUtcNowOutsideParameters()
    {
        // Arrange
        var infraDir = Path.Combine(_repoRoot, "deploy-infra");
        
        if (!Directory.Exists(infraDir))
        {
            return;
        }

        var bicepFiles = Directory.GetFiles(infraDir, "*.bicep", SearchOption.AllDirectories);

        foreach (var bicepFile in bicepFiles)
        {
            var content = File.ReadAllText(bicepFile);
            var lines = content.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                
                // Skip comments
                if (line.StartsWith("//") || line.StartsWith("*"))
                {
                    continue;
                }

                // Check for utcNow() usage
                if (line.Contains("utcNow()"))
                {
                    // This is acceptable only in parameter default values
                    // Check if this line is a parameter declaration with utcNow()
                    var isParameterLine = line.StartsWith("param ") && line.Contains("utcNow()");
                    
                    isParameterLine.Should().BeTrue(
                        $"utcNow() should only be used in parameter default values in {Path.GetFileName(bicepFile)} line {i + 1}. Found: {line}");
                }
            }
        }
    }

    [Fact]
    public void Repository_HasNoBashScripts()
    {
        // Arrange
        var bashExtensions = new[] { ".sh", ".bash" };

        foreach (var ext in bashExtensions)
        {
            var bashFiles = Directory.GetFiles(_repoRoot, $"*{ext}", SearchOption.AllDirectories)
                .Where(f => !f.Contains(".git")) // Exclude .git directory
                .ToList();

            // Assert
            bashFiles.Should().BeEmpty(
                $"Repository should not contain {ext} files (use PowerShell instead). Found: {string.Join(", ", bashFiles.Select(Path.GetFileName))}");
        }
    }

    [Fact]
    public void PowerShellScripts_PassScriptAnalyzer()
    {
        // Skip if PSScriptAnalyzer is not available
        if (!IsPSScriptAnalyzerAvailable())
        {
            return;
        }

        // Arrange
        var psFiles = new[]
        {
            Path.Combine(_repoRoot, "deploy-all.ps1"),
            Path.Combine(_repoRoot, "deploy-infra", "deploy.ps1"),
            Path.Combine(_repoRoot, "deploy-app", "deploy.ps1")
        }.Where(File.Exists).ToList();

        foreach (var psFile in psFiles)
        {
            // Act
            var result = RunCommand("pwsh", 
                $"-NoProfile -Command \"Invoke-ScriptAnalyzer -Path '{psFile}' -Severity Error | Format-Table -AutoSize\"");

            // Assert
            result.Output.Should().NotContain("Error", 
                $"PowerShell script {Path.GetFileName(psFile)} should pass PSScriptAnalyzer with no errors. Output: {result.Output}");
        }
    }

    [Fact]
    public void DeployAllScript_UsesHashtableSplatting()
    {
        // Arrange
        var deployAllScript = Path.Combine(_repoRoot, "deploy-all.ps1");
        
        if (!File.Exists(deployAllScript))
        {
            return;
        }

        var content = File.ReadAllText(deployAllScript);

        // Assert
        content.Should().Contain("@{", "deploy-all.ps1 should use hashtable splatting (@{})");
        content.Should().NotContain("@(", "deploy-all.ps1 should not use array splatting (@())");
    }

    [Fact]
    public void DeployAllScript_SplatsCorrectly()
    {
        // Arrange
        var deployAllScript = Path.Combine(_repoRoot, "deploy-all.ps1");
        
        if (!File.Exists(deployAllScript))
        {
            return;
        }

        var content = File.ReadAllText(deployAllScript);

        // Assert - Look for the @infraArgs or similar pattern
        System.Text.RegularExpressions.Regex.IsMatch(content, @"@\w+Args", System.Text.RegularExpressions.RegexOptions.Multiline)
            .Should().BeTrue("deploy-all.ps1 should splat arguments correctly using @variableName");
    }

    [Fact]
    public void BicepModules_ExistForAllComponents()
    {
        // Arrange
        var modulesDir = Path.Combine(_repoRoot, "deploy-infra", "modules");
        
        if (!Directory.Exists(modulesDir))
        {
            return;
        }

        var expectedModules = new[]
        {
            "app-service.bicep",
            "azure-sql.bicep",
            "managed-identity.bicep",
            "monitoring.bicep"
        };

        foreach (var module in expectedModules)
        {
            var modulePath = Path.Combine(modulesDir, module);
            
            // Assert
            File.Exists(modulePath).Should().BeTrue(
                $"Module {module} should exist in deploy-infra/modules");
        }
    }

    // Helper methods
    private bool IsAzCliAvailable()
    {
        try
        {
            var result = RunCommand("az", "version");
            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private bool IsPSScriptAnalyzerAvailable()
    {
        try
        {
            var result = RunCommand("pwsh", 
                "-NoProfile -Command \"Get-Module -ListAvailable -Name PSScriptAnalyzer\"");
            return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.Output);
        }
        catch
        {
            return false;
        }
    }

    private (int ExitCode, string Output, string Error) RunCommand(string command, string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = command,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        if (process == null)
        {
            return (-1, "", "Failed to start process");
        }

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return (process.ExitCode, output, error);
    }
}
