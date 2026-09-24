using ApiTestGenerationAgent.Tools;

namespace ApiTestGenerationAgent.Agent;

public sealed class AgentRunner
{
    private readonly OllamaClient _client;
    private readonly ToolExecutor _tools;
    private readonly string _model;

    public AgentRunner(OllamaClient client, ToolExecutor tools, string model)
    {
        _client = client;
        _tools = tools;
        _model = model;
    }

    public async Task<string> RunAsync()
    {
        var messages = new List<OllamaMessage>
        {
            new()
            {
                Role = "system",
                Content = """
                You are an API test-generation agent.

                REQUIRED WORKFLOW:
                1. Call read_openapi.
                2. Then call create_test_plan.
                3. Then call run_tests.
                4. If tests fail, call create_test_plan again with a corrected plan, then run_tests again.
                5. Stop after tests pass.

                IMPORTANT:
                - Never generate C# source code yourself.
                - create_test_plan accepts structured JSON test cases.
                - Use only paths and response codes documented in the OpenAPI specification.
                - Keep the plan small: at most 1-2 tests per API operation.
                """
            },
            new()
            {
                Role = "user",
                Content = "Start by calling read_openapi."
            }
        };

        string lastTool = "";

        for (int turn = 1; turn <= 6; turn++)
        {
            Console.WriteLine($"Agent turn {turn}...");

            var assistant = await _client.ChatAsync(
                _model,
                messages,
                ToolDefinitions.All);

            messages.Add(assistant);

            if (assistant.ToolCalls is { Count: > 0 })
            {
                foreach (var call in assistant.ToolCalls)
                {
                    lastTool = call.Function.Name;
                    Console.WriteLine($"  -> tool: {lastTool}");

                    var result = await _tools.ExecuteAsync(
                        lastTool,
                        call.Function.Arguments);

                    messages.Add(new OllamaMessage
                    {
                        Role = "tool",
                        ToolName = lastTool,
                        Content = result
                    });

                    if (lastTool == "read_openapi")
                    {
                        messages.Add(new OllamaMessage
                        {
                            Role = "user",
                            Content = "Now call create_test_plan. Do not generate C# code."
                        });
                    }
                    else if (lastTool == "create_test_plan")
                    {
                        messages.Add(new OllamaMessage
                        {
                            Role = "user",
                            Content = "Now call run_tests."
                        });
                    }
                    else if (lastTool == "run_tests")
                    {
                        if (result.Contains("TESTS_PASSED", StringComparison.Ordinal))
                            return result;

                        messages.Add(new OllamaMessage
                        {
                            Role = "user",
                            Content = "Tests failed. Review the result and call create_test_plan with a corrected plan."
                        });
                    }
                }

                continue;
            }

            // Some small local models return only <think></think> or empty content.
            var clean = Clean(assistant.Content);
            if (string.IsNullOrWhiteSpace(clean))
            {
                messages.Add(new OllamaMessage
                {
                    Role = "user",
                    Content = lastTool switch
                    {
                        "read_openapi" => "Call create_test_plan now.",
                        "create_test_plan" => "Call run_tests now.",
                        "run_tests" => "Call create_test_plan to correct the failing tests.",
                        _ => "Call the next required tool."
                    }
                });

                continue;
            }

            messages.Add(new OllamaMessage
            {
                Role = "user",
                Content = lastTool switch
                {
                    "read_openapi" => "Do not finish. Call create_test_plan.",
                    "create_test_plan" => "Do not finish. Call run_tests.",
                    _ => "Continue the workflow using the required tool."
                }
            });
        }

        return "Agent stopped after the maximum number of turns. Check generated-tests/.";
    }

    private static string Clean(string value) =>
        System.Text.RegularExpressions.Regex
            .Replace(value ?? "", @"<think>\s*</think>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
            .Trim();
}
