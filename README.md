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





