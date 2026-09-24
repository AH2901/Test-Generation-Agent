using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace ApiTestGenerationAgent.Tools;

public sealed class ToolExecutor
{
    private readonly string _specPath;
    private readonly string _outputDirectory;

    public ToolExecutor(string specPath, string outputDirectory)
    {
        _specPath = specPath;
        _outputDirectory = outputDirectory;
    }

    public Task<string> ExecuteAsync(string name, JsonElement args) => name switch
    {
        "read_openapi" => ReadOpenApiAsync(),
        "create_test_plan" => CreateTestPlanAsync(args),
        "run_tests" => RunTestsAsync(),
        _ => Task.FromResult($"Unknown tool: {name}")
    };

    private async Task<string> ReadOpenApiAsync()
    {
        var text = await File.ReadAllTextAsync(_specPath);
        using var doc = JsonDocument.Parse(text);
        var root = doc.RootElement;

        var version = root.TryGetProperty("openapi", out var v)
            ? v.GetString() ?? "unknown"
            : "unknown";

        var sb = new StringBuilder();
        sb.AppendLine("OPENAPI SUMMARY");
        sb.AppendLine($"Version: {version}");

        if (!root.TryGetProperty("paths", out var paths) || paths.ValueKind != JsonValueKind.Object)
            return sb.AppendLine("No paths found.").ToString();

        foreach (var path in paths.EnumerateObject())
        {
            foreach (var operation in path.Value.EnumerateObject())
            {
                if (operation.NameEquals("parameters"))
                    continue;

                sb.AppendLine($"PATH: {path.Name}");
                sb.AppendLine($"METHOD: {operation.Name.ToUpperInvariant()}");

                if (operation.Value.TryGetProperty("responses", out var responses))
                {
                    foreach (var response in responses.EnumerateObject())
                        sb.AppendLine($"RESPONSE: {response.Name}");
                }
            }
        }

        return sb.ToString();
    }

    private async Task<string> CreateTestPlanAsync(JsonElement args)
    {
        if (!args.TryGetProperty("testCases", out var testCases) ||
            testCases.ValueKind != JsonValueKind.Array)
        {
            return "ERROR: create_test_plan requires a testCases array.";
        }

        var valid = new List<TestCase>();

        foreach (var item in testCases.EnumerateArray())
        {
            if (!item.TryGetProperty("path", out var pathElement) ||
                !item.TryGetProperty("method", out var methodElement) ||
                !item.TryGetProperty("expectedStatus", out var statusElement) ||
                !item.TryGetProperty("name", out var nameElement))
                continue;

            var path = pathElement.GetString();
            var method = methodElement.GetString();
            var name = nameElement.GetString();

            if (string.IsNullOrWhiteSpace(path) ||
                string.IsNullOrWhiteSpace(method) ||
                string.IsNullOrWhiteSpace(name) ||
                !statusElement.TryGetInt32(out var status))
                continue;

            valid.Add(new TestCase(path, method.ToUpperInvariant(), status, SanitizeIdentifier(name)));
        }

        if (valid.Count == 0)
            return "ERROR: No valid test cases supplied.";

        valid = valid.Take(8).ToList();

        Directory.CreateDirectory(_outputDirectory);

        await File.WriteAllTextAsync(
            Path.Combine(_outputDirectory, "GeneratedApiTests.csproj"), ProjectFile());

        await File.WriteAllTextAsync(
            Path.Combine(_outputDirectory, "GeneratedApiTests.cs"), GenerateSource(valid));

        return $"SUCCESS: Generated {valid.Count} test(s).";
    }

    private async Task<string> RunTestsAsync()
    {
        var project = Path.Combine(_outputDirectory, "GeneratedApiTests.csproj");
        if (!File.Exists(project))
            return "ERROR: Test project not found. Call create_test_plan first.";

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = "test GeneratedApiTests.csproj --nologo --verbosity minimal",
            WorkingDirectory = Path.GetFullPath(_outputDirectory),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        var output = ((await outputTask) + "\n" + (await errorTask)).Trim();
        if (output.Length > 5000)
            output = output[^5000..];

        return process.ExitCode == 0
            ? "TESTS_PASSED\n" + output
            : "TESTS_FAILED\n" + output;
    }

    private static string ProjectFile() => """
    <Project Sdk="Microsoft.NET.Sdk">
      <PropertyGroup>
        <TargetFramework>net8.0</TargetFramework>
        <ImplicitUsings>enable</ImplicitUsings>
        <Nullable>enable</Nullable>
      </PropertyGroup>
      <ItemGroup>
        <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
        <PackageReference Include="xunit" Version="2.9.3" />
        <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
      </ItemGroup>
    </Project>
    """;

    private static string GenerateSource(IEnumerable<TestCase> tests)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System.Net;");
        sb.AppendLine("using System.Net.Http;");
        sb.AppendLine("using Xunit;");
        sb.AppendLine();
        sb.AppendLine("namespace GeneratedApiTests;");
        sb.AppendLine();
        sb.AppendLine("public sealed class GeneratedApiTests");
        sb.AppendLine("{");

        foreach (var test in tests)
        {
            var path = test.Path.Replace("\"", "\\\"");
            var httpMethod = test.Method switch
            {
                "GET" => "HttpMethod.Get",
                "POST" => "HttpMethod.Post",
                "PUT" => "HttpMethod.Put",
                "PATCH" => "HttpMethod.Patch",
                "DELETE" => "HttpMethod.Delete",
                _ => "HttpMethod.Get"
            };

            var status = ToStatusName(test.ExpectedStatus);

            sb.AppendLine("    [Fact]");
            sb.AppendLine($"    public async Task {test.Name}()");
            sb.AppendLine("    {");
            sb.AppendLine($"        using var handler = new FakeHandler(HttpStatusCode.{status});");
            sb.AppendLine("        using var client = new HttpClient(handler) { BaseAddress = new Uri(\"https://localhost\") };");
            sb.AppendLine($"        using var request = new HttpRequestMessage({httpMethod}, \"{path}\");");
            sb.AppendLine("        using var response = await client.SendAsync(request);");
            sb.AppendLine($"        Assert.Equal(HttpStatusCode.{status}, response.StatusCode);");
            sb.AppendLine("        Assert.Equal(1, handler.CallCount);");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.AppendLine("    private sealed class FakeHandler : HttpMessageHandler");
        sb.AppendLine("    {");
        sb.AppendLine("        private readonly HttpStatusCode _status;");
        sb.AppendLine("        public int CallCount { get; private set; }");
        sb.AppendLine("        public FakeHandler(HttpStatusCode status) => _status = status;");
        sb.AppendLine("        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)");
        sb.AppendLine("        {");
        sb.AppendLine("            CallCount++;");
        sb.AppendLine("            return Task.FromResult(new HttpResponseMessage(_status));");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string ToStatusName(int status) => status switch
    {
        200 => "OK",
        201 => "Created",
        202 => "Accepted",
        204 => "NoContent",
        400 => "BadRequest",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "NotFound",
        409 => "Conflict",
        422 => "UnprocessableEntity",
        500 => "InternalServerError",
        _ => "OK"
    };

    private static string SanitizeIdentifier(string value)
    {
        var result = new string(value.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        if (string.IsNullOrWhiteSpace(result)) result = "GeneratedApiTest";
        if (char.IsDigit(result[0])) result = "Test" + result;
        return result;
    }

    private sealed record TestCase(string Path, string Method, int ExpectedStatus, string Name);
}
