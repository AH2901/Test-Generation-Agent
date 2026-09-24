using ApiTestGenerationAgent.Agent;
using ApiTestGenerationAgent.Tools;

var options = AppOptions.Parse(args);

Console.WriteLine("API Test Generation Agent");
Console.WriteLine("=========================");
Console.WriteLine($"Ollama: {options.OllamaUrl}");
Console.WriteLine($"Model : {options.Model}");
Console.WriteLine($"Spec  : {options.SpecPath}");
Console.WriteLine();

if (!File.Exists(options.SpecPath))
{
    Console.Error.WriteLine($"OpenAPI file not found: {options.SpecPath}");
    Environment.ExitCode = 1;
    return;
}

var tools = new ToolExecutor(options.SpecPath, options.OutputDirectory);
var client = new OllamaClient(options.OllamaUrl);

try
{
    await client.CheckHealthAsync(options.Model);
    var runner = new AgentRunner(client, tools, options.Model);

    var result = await runner.RunAsync();
    Console.WriteLine();
    Console.WriteLine("FINAL REPORT");
    Console.WriteLine("============");
    Console.WriteLine(result);
}
catch (Exception ex)
{
    Console.Error.WriteLine();
    Console.Error.WriteLine("ERROR: " + ex.Message);
    Environment.ExitCode = 1;
}

internal sealed record AppOptions(
    string OllamaUrl,
    string Model,
    string SpecPath,
    string OutputDirectory)
{
    public static AppOptions Parse(string[] args)
    {
        string ollamaUrl = "http://localhost:11434";
        string model = "qwen3:4b-instruct-2507-q4_K_M";
        string specPath = Path.Combine("sample", "openapi.json");
        string outputDirectory = Path.Combine("generated-tests");

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--model" when i + 1 < args.Length:
                    model = args[++i];
                    break;

                case "--spec" when i + 1 < args.Length:
                    specPath = args[++i];
                    break;

                case "--output" when i + 1 < args.Length:
                    outputDirectory = args[++i];
                    break;

                case "--ollama" when i + 1 < args.Length:
                    ollamaUrl = args[++i];
                    break;

                case "--help":
                case "-h":
                    PrintHelp();
                    Environment.Exit(0);
                    break;
            }
        }

        return new AppOptions(ollamaUrl, model, specPath, outputDirectory);
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
        Usage:
          dotnet run
          dotnet run -- --model qwen3:1.7b
          dotnet run -- --spec sample/openapi.json --output generated-tests

        Options:
          --model   Ollama model. Default: qwen3:1.7b
          --spec    OpenAPI JSON path. Default: sample/openapi.json
          --output  Generated test directory. Default: generated-tests
          --ollama  Ollama base URL. Default: http://localhost:11434
        """);
    }
}
