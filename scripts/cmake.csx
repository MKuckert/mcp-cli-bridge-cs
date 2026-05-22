Host.Name("cmake_prepare");
Host.Description("Configures the CMake project.");

var type = Host.Param("config", "Debug", "Build configuration (Debug/Release)");

Host.OnExecute(async execute =>
{
    // TODO Use arrays for arguments instead of string interpolation
    // TODO Use argument escaping

    var value = execute["config"];
    var result = await Host.RunShell($"cmake -B build -DCMAKE_BUILD_TYPE={value}");

    // TODO Pass exitcode back, not stdout only
    //if (result.ExitCode != 0)
    //    return $"Config failed: {result.StdErr}";
    //return $"Config successful: {result.StdOut}";
    return "OK";
});
