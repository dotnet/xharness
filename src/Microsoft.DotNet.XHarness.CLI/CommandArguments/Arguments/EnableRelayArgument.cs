namespace Microsoft.DotNet.XHarness.CLI.CommandArguments;

/// <summary>
/// Enable xharness to function as a relay when communicating with the appliecation
/// </summary>
internal class EnableRelayArgument : SwitchArgument
{
    public EnableRelayArgument() : base("enable-relay", "Allow to communicate with the launched application", false)
    {
    }
}
