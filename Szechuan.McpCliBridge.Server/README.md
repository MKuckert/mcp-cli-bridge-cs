# MCP CLI Bridge

## Implementation

The MCP server is built as a self-contained application and does not require the .NET runtime to be installed on the target machine.

It's build for:
* `win-x64`
* `win-arm64`
* `osx-arm64`
* `linux-x64`
* `linux-arm64`
* `linux-musl-x64`

It leverages the [MCP C# SDK](https://modelcontextprotocol.github.io/csharp-sdk)

<!--
See [aka.ms/nuget/mcp/guide](https://aka.ms/nuget/mcp/guide) for the full guide.
-->

<!--
## Publishing to NuGet.org

1. Run `dotnet pack -c Release` to create the NuGet package
2. Publish to NuGet.org with `dotnet nuget push bin/Release/*.nupkg --api-key <your-api-key> --source https://api.nuget.org/v3/index.json`
-->
