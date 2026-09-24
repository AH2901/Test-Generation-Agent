namespace ApiTestGenerationAgent.Tools;

public static class ToolDefinitions
{
    public static object[] All =>
    [
        new
        {
            type = "function",
            function = new
            {
                name = "read_openapi",
                description = "Read and summarize the OpenAPI specification. Use this first.",
                parameters = new
                {
                    type = "object",
                    properties = new { },
                    required = Array.Empty<string>()
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "create_test_plan",
                description = "Create structured xUnit API test cases from the OpenAPI specification. Never provide C# code.",
                parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        testCases = new
                        {
                            type = "array",
                            description = "Small list of API contract tests.",
                            items = new
                            {
                                type = "object",
                                properties = new
                                {
                                    path = new { type = "string" },
                                    method = new { type = "string" },
                                    expectedStatus = new { type = "integer" },
                                    name = new { type = "string" }
                                },
                                required = new[] { "path", "method", "expectedStatus", "name" }
                            }
                        }
                    },
                    required = new[] { "testCases" }
                }
            }
        },
        new
        {
            type = "function",
            function = new
            {
                name = "run_tests",
                description = "Run the generated xUnit test project using dotnet test.",
                parameters = new
                {
                    type = "object",
                    properties = new { },
                    required = Array.Empty<string>()
                }
            }
        }
    ];
}
