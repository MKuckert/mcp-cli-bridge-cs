Name("echo_test");
Description("Echoes a message.");

var msg = Param("message", "Hello World", "The message to echo");

OnExecute(async () => {
    var result = await RunShell("echo", msg.Value);
    return result.StdOut;
});
