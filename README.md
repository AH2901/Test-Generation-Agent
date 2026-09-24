# API Test Generation Agent

A small, local-first **C#/.NET 8 agentic AI project** that turns an OpenAPI document into executable xUnit tests.

The agent uses a local Ollama model and tools to:

1. Read an OpenAPI specification.
2. Generate C# xUnit tests.
3. Write the generated test source.
4. Run `dotnet test`.
5. Inspect failures through the tool result.
6. Repair the generated test file when needed.
7. Return a final summary.

No paid LLM API is required.

## Why this project

This demonstrates practical AI-native software engineering concepts:

- Agentic workflow
- LLM integration
- Tool/function calling
- Prompt engineering
- Context engineering
- OpenAPI-driven test generation
- Automated test execution
- Self-correction loop
- C#/.NET
- Local-first AI development

It is deliberately small enough to build and explain in a portfolio interview.

## Prerequisites

- Windows, macOS, or Linux
- .NET 8 SDK
- Ollama
- Internet access once to download the local model and NuGet test packages

Ollama's `/api/chat` supports tool definitions and tool calls. Qwen3 models are listed by Ollama as tool-capable models.

## 1. Install and start Ollama

Install Ollama from its official website, then confirm the CLI works:

```powershell
ollama --version
```

Pull the small model used by default:

```powershell
ollama pull qwen3:1.7b
```

Verify it:

```powershell
ollama run qwen3:1.7b
```

Type:

```text
hello
```

Then exit the model interaction.

### Lower memory option

`qwen3:0.6b` is smaller. Change the command to:

```powershell
ollama pull qwen3:0.6b
dotnet run -- --model qwen3:0.6b
```

The 1.7B model is the default because it is still small while generally being more useful for code generation.

## 2. Run the agent

From the project folder:

```powershell
dotnet restore
dotnet run
```

The agent reads:

```text
sample/openapi.json
```

and writes generated tests under:

```text
generated-tests/
```

You should see output similar to:

```text
API Test Generation Agent
=========================
Ollama: http://localhost:11434
Model : qwen3:1.7b
Spec  : sample/openapi.json

Agent turn 1...
  -> tool: read_openapi
Agent turn 2...
  -> tool: write_test_file
Agent turn 3...
  -> tool: run_tests
Agent finished.

FINAL REPORT
============
...
```

## 3. Use your own OpenAPI document

Replace the sample:

```powershell
dotnet run -- --spec "C:\path\to\openapi.json"
```

Or choose another output folder:

```powershell
dotnet run -- --spec "C:\path\to\openapi.json" --output "generated-tests"
```

## How the agent works

```text
                 +-------------------+
                 |       User        |
                 +---------+---------+
                           |
                           v
                 +-------------------+
                 |   Ollama LLM      |
                 |    Qwen3 1.7B     |
                 +---------+---------+
                           |
                     tool calling
                           |
             +-------------+-------------+
             |             |             |
             v             v             v
     read_openapi   write_test_file   run_tests
             |             |             |
             v             v             v
         OpenAPI       C# xUnit       dotnet test
             |                           |
             +-------------+-------------+
                           |
                         result
                           |
                           v
                    repair if needed
```

## Important design choice

The agent does **not** call a real production API.

Instead, it generates deterministic tests around mocked `HttpClient` responses. That keeps the demo:

- free
- local
- fast
- safe to run
- independent of credentials
- easy to explain in an interview

The important engineering demonstration is the agent loop:

```text
understand spec
    -> generate tests
    -> execute tests
    -> observe result
    -> repair
```

## Portfolio talking points

A good interview explanation is:

> I built a local agentic test-generation workflow in C# using Ollama. The LLM was given tools to inspect an OpenAPI specification, create xUnit tests, and execute `dotnet test`. The workflow uses the test output as feedback so the agent can repair generated code instead of treating the first generation as final.

Then explain the limitations:

> I kept the implementation deliberately small. It validates generated test code locally rather than invoking a live API, so the next production-oriented extension would add an API environment or containerized test target, stronger schema validation, and CI integration.

## Resume version

**API Test Generation Agent | C#, .NET 8, Ollama, Qwen3, OpenAPI, xUnit**

- Built a local agentic test-generation workflow that converts OpenAPI specifications into executable C# xUnit tests.
- Integrated a local LLM through Ollama and implemented tool/function calling for specification analysis, test-file generation, and automated `dotnet test` execution.
- Designed a feedback loop that uses build/test results to trigger test-code repair before producing the final report.
- Applied prompt and context engineering to constrain generated tests to documented API paths, parameters, request bodies, and responses.
- Demonstrated AI-native SDLC automation without requiring paid cloud LLM APIs.

## Suggested GitHub repository name

```text
api-test-generation-agent
```

## Suggested commit sequence

```text
git init
git add .
git commit -m "Initial C# agentic API test generator"

git add .
git commit -m "Add OpenAPI-driven test generation tools"

git add .
git commit -m "Add test execution and agent repair loop"
```

## .gitignore

Create a `.gitignore` containing:

```gitignore
bin/
obj/
generated-tests/
.vs/
*.user
*.suo
```

## Next extensions

Good follow-on improvements, once the base project works:

- Add a `read_project_files` tool.
- Add RAG over coding standards or API guidelines.
- Add a second specialist agent for test review.
- Add Git diff input so the agent generates tests only for changed endpoints.
- Add GitHub Actions to execute the generated test workflow.
- Add an optional Azure OpenAI provider alongside Ollama.

Keep the first version small. The portfolio value comes from being able to explain the **agent loop, tools, context, validation, and tradeoffs**, not from having a large codebase.
