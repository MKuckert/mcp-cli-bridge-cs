# Project Plan: MCP CLI-Bridge

## 🎯 Objective
Create an MCP (Model Context Protocol) server that dynamically loads C# scripts (`.csx`), exposing them as structured tools to LLMs. It executes CLI programs within a specific target directory, supports hot-reloading for live script updates, and ensures robust parameter mapping and safety mechanisms.

## 🛠 Requirements & Decisions
- **Frameworks:** .NET (latest), Roslyn (`Microsoft.CodeAnalysis.CSharp.Scripting`), `CliWrap`
- **Chosen Libraries:** `System.CommandLine` (CLI Parsing), `ModelContextProtocol` (Official C# SDK)
- **Error Handling Strategy:** 
  - Broken `.csx` scripts on startup: Halt the boot process immediately with a fatal error.
  - Broken `.csx` scripts during hot-reload: Reject the update, maintain the last known good state of the tool, and log a loud warning with the compilation error. Tools must never magically vanish.
  - CLI processes (`RunShell`) that timeout are forcefully killed (process tree); the task returns an error indicating the timeout.
  - CLI processes returning non-zero exit codes: The standard error (STDERR) and the non-zero exit code are captured and returned directly to the MCP client as an error payload (no silent failures).
  - A missing or unreadable target directory (`--dir`) causes an immediate fatal error and process exit.
  - Concurrent execution during hot-reloading: Existing executions are allowed to finish.

## 🏛 Architecture & Design Guidelines
- **CLI Layer:** Use `System.CommandLine` with `CliRootCommand` (fluent API) to parse arguments. Set the exit code via the `InvocationContext`.
- **Orchestration Layer:** A `ScriptOrchestrator` should bridge CLI parsing, script discovery, Roslyn initialization, and MCP Server setup.
- **State Management:** Keep a single `ScriptState<T>` for the execution session using a custom `ScriptContext` (which acts as the globals). Remember that `ScriptState` is not thread-safe.
- **MCP Integration:** Register request handlers (`tools/list`, `tools/call`) *before* calling `server.Start(transport)`. The server transport blocks on `Start()`. 

## 🏗 Implementation Steps
> Status Markers: [ ] Open, [/] In Progress, [x] Completed (By the Reviewer only!)

- [/] **Task 1: Core Domain & Script Context**
  - **Description:** Implement `McpScriptHost` (the globals object / context), `McpToolContainer`, and `McpParameter`. These form the Roslyn globals DSL (`Name()`, `Description()`, `Param()`, `OnExecute()`). Ensure `ScriptContext` exposes the required APIs for the orchestrator.
  - **Review Criteria:** Models compile successfully and allow defining all required script metadata cleanly.
- [/] **Task 2: CLI Bootstrapping & Directory Validation**
  - **Description:** Integrate `System.CommandLine` to parse `--dir`, `--scripts`, and `--watch`. Implement validation to exit immediately if `--dir` is missing/unreadable. Set process CWD.
  - **Review Criteria:** CLI runs, parses arguments correctly, and fails fast if the target directory is invalid.
- [/] **Task 3: Script Discovery Engine & Orchestrator**
  - **Description:** Implement the `ScriptDiscoverer` and `ScriptOrchestrator` to scan the `--scripts` folder. Compile and execute `*.csx` files via `CSharpScript.RunAsync` into a `ScriptState`. Fail loud and halt startup if any script fails to compile.
  - **Review Criteria:** Valid scripts are discovered and transformed into `McpToolContainer`s; invalid scripts halt the boot process.
- [/] **Task 4: MCP Server Integration**
  - **Description:** Implement the `ModelContextProtocol` server in the orchestrator. Configure Stdio transport. Map `McpToolContainer` metadata to MCP tool schemas and register `tools/list` and `tools/call` handlers before `Start()`.
  - **Review Criteria:** MCP Client can connect, list tools correctly, and handle graceful shutdown.
- [/] **Task 5: Execution Engine & CLI Wrap**
  - **Description:** Implement `RunShell` in `McpScriptHost` using `CliWrap`. Add process timeouts (kill process tree), execute the mapped logic upon `tools/call`, and return results/errors. Capture STDERR and non-zero exit codes to return as MCP error payloads.
  - **Review Criteria:** Tools execute successfully, parameters map correctly, timeouts kill processes, and non-zero exit codes return clear errors to the client.
- [/] **Task 6: Hot-Reloading (Watcher)**
  - **Description:** Implement `FileSystemWatcher` for the scripts directory if `--watch` is specified. Debounce events by a constant amount (500ms). Re-run discovery for changed files. If compilation fails, reject update, keep last good state, log loud error. Send `notifications/tools/list_changed` if successful.
  - **Review Criteria:** Modifying a script updates the tool registry; syntax errors keep the old state; ongoing executions are not aborted.

## 🛡 Edge Case & Safety Checklist
- [ ] Target directory missing or unreadable -> Immediate process exit.
- [ ] Rapid file saves during `--watch` -> Debounced correctly (500ms).
- [ ] Tool updated while currently executing -> Let current execution finish undisturbed.
- [ ] Tool update contains compile errors -> Keep last known good state, scream into logs.
- [ ] CLI command hangs -> Process tree killed after timeout, error returned.
- [ ] CLI command returns non-zero -> STDERR and exit code surfaced to MCP client as error.
- [ ] Script throws unhandled exception -> Server remains alive, error returned to MCP client.
- [ ] Broken script on startup -> Boot process halted entirely.

## 📝 Review Log (Mode 1: Plan Review)
- **Round 1:** Approved (Architect / Initial Draft)
- **Round 2:** Approved (Librarian enhancements merged)
- **Round 3:** Rejected by Reviewer (Silent errors on hot-reload/startup and missing non-zero exit code handling).
- **Round 4:** Fixed and Pending Review.

## 🚦 Final Status (Mode 2: Code Review)
- [Pending Builder Phase]
