namespace SharpAI.Tui.Localization
{
    using System.Collections.Generic;

    internal static class OperatorKeys
    {
        internal static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>
        {
            ["+ Add entry"] = "settings.addOverride", ["+ Add row"] = "common.add", ["- Remove"] = "common.delete", ["- Remove row "] = "common.delete",
            ["Add mapping"] = "admin.map", ["Apply filters"] = "common.apply", ["Cancel"] = "common.cancel", ["Clear drilldown"] = "requestHistory.clearRange",
            ["Configuration: "] = "library.configurationSelector", ["Copy "] = "common.copy", 
            ["Copy result"] = "common.copy", ["Copy token"] = "common.copy", ["Create scope"] = "admin.grant", 
            ["Delete selected local model"] = "models.deleteTitle", ["Dismiss"] = "common.close", ["Edit configuration"] = "library.editConfiguration", ["Edit prompt"] = "library.editPrompt",
            ["Enter a repository name or full HTTP(S) model URL."] = "models.pullHelp", ["Inspect / actions"] = "admin.details", ["Invalid model URL."] = "models.invalidUrl",
            ["Model: "] = "playground.model", ["Passwords do not match."] = "admin.passwordMismatch", ["Permission mappings"] = "admin.mappings", ["Pull"] = "models.pull",
            ["Remove scope"] = "common.delete", ["Reveal token"] = "admin.revealToken", ["Rotate token"] = "admin.regenerateToken", ["Saved. Some changes require a server restart."] = "settings.restartNote",
            ["Select a model."] = "chat.selectModel", ["Select a saved model configuration."] = "library.configurationSelectorHelp", ["System prompt: "] = "library.promptSelector",
            ["Tenant selection requires a platform administrator."] = "admin.denied", ["Turn details"] = "chat.telemetry", ["Unload selected resident model"] = "models.unloadTitle",
            ["Protected records cannot be changed."] = "admin.roleHelp"
        };
    }
}
