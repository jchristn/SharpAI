namespace SharpAI.Tui
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json.Nodes;
    using System.Threading.Tasks;
    using SharpAI.Client;
    using SharpAI.Tui.Services;
    using SharpAI.Tui.Widgets;
    using TUIKit.Layout;
    using TUIKit.Widgets;

    internal sealed partial class SharpAITuiApp
    {
        private string TenantPath { get { return "/v1.0/tenants/" + ServerClient.Segment(_Profile.Tenant ?? JsonData.Text(_Context, "tenantId")); } }
        private bool CanAccess(string section, bool write)
        {
            if (JsonData.Flag(_Context, "isAdmin")) return true;
            string resource = section switch { "tenants" => "Tenant", "users" => "User", "credentials" => "Credential", "roles" => "Role", "permissions" => "Permission", "assignments" => "Assignment", "sessions" => "Session", _ => "Audit" };
            return JsonData.Flag(_Context?["capabilities"]?[resource], write ? "write" : "read");
        }
        private void Access(string section)
        {
            string tenant = _Profile.Tenant ?? JsonData.Text(_Context, "tenantId");
            if (!CanAccess(section, false)) { ShowText("Access", _Localizer.Text("admin.denied")); return; }
            if (section != "tenants" && string.IsNullOrEmpty(tenant)) { ShowText("Access", "Select a target tenant using Commands > Select tenant."); return; }
            string basePath = section == "tenants" ? "/v1.0/tenants" : section == "audit" ? Scoped("/v1.0/api/audit") : TenantPath + "/" + section;
            DataTable<JsonNode> table = Table(AccessColumns(section));
            TabStrip tabs = new TabStrip(); string[] sections = { "tenants", "users", "credentials", "roles", "permissions", "assignments", "sessions", "audit" };
            foreach (string item in sections) tabs.Add(_Localizer.Text("admin." + item)); tabs.ActiveIndex = Array.IndexOf(sections, section);
            tabs.ActiveTabChanged += (sender, change) => { if (tabs.ActiveIndex >= 0) Navigate("/access/" + sections[tabs.ActiveIndex]); };
            LoadTable(table, basePath, row => { AccessRecord(section, basePath, row); return Task.CompletedTask; });
            ButtonRow actions = new ButtonRow();
            if (section is not ("audit" or "sessions") && CanAccess(section, true)) actions.Add(ButtonFor("Create", () => AccessEdit(section, basePath, null)));
            actions.Add(ButtonFor("Inspect / actions", () => { if (table.SelectedRow != null) AccessRecord(section, basePath, table.SelectedRow); }));
            if (section == "assignments") actions.Add(ButtonFor("Credential scopes", () => Pick(TenantPath + "/credentials", "Select credential", row => AssignmentList(JsonData.Id(row)))));
            Show(new FramedStack().Add(tabs, StackSize.Fixed(3)).Add(ViewTable(table), StackSize.Weighted(1)).Add(actions, StackSize.Fixed(3)), "Access");
        }
        private static string[] AccessColumns(string section)
        {
            return section switch
            {
                "tenants" => new[] { "Guid", "Name", "Active", "IsProtected", "CreatedUtc" },
                "users" => new[] { "Guid", "FirstName", "LastName", "Email", "IsAdmin", "IsTenantAdmin", "Active", "CreatedUtc" },
                "credentials" => new[] { "Guid", "Name", "UserGuid", "Active", "ExpiresUtc", "CreatedUtc" },
                "roles" => new[] { "Guid", "Name", "Active", "IsBuiltIn", "CreatedUtc" },
                "permissions" => new[] { "Guid", "Name", "Effect", "ResourceTypes", "OperationTypes", "Active", "IsBuiltIn" },
                "assignments" => new[] { "Guid", "UserGuid", "RoleGuid", "ResourceScope", "ResourceGuid", "InheritsToChildren" },
                "sessions" => new[] { "Guid", "UserGuid", "CreatedUtc", "ExpiresUtc" },
                _ => new[] { "Guid", "EventType", "PrincipalGuid", "Path", "Outcome", "CreatedUtc" }
            };
        }
        private static JsonObject AccessDraft(string section, JsonNode? row)
        {
            JsonObject draft = section switch
            {
                "users" => new JsonObject { ["Email"] = "", ["FirstName"] = "", ["LastName"] = "", ["IsTenantAdmin"] = false },
                "credentials" => new JsonObject { ["Name"] = "", ["ExpiresUtc"] = "", ["ClearExpiration"] = true },
                "permissions" => new JsonObject { ["Name"] = "", ["ResourceTypes"] = new JsonArray("Model"), ["OperationTypes"] = new JsonArray("Read"), ["Effect"] = "Permit", ["Active"] = true },
                "assignments" => new JsonObject { ["UserGuid"] = "", ["RoleGuid"] = "", ["ResourceScope"] = "Tenant", ["ResourceGuid"] = "", ["InheritsToChildren"] = true },
                _ => new JsonObject { ["Name"] = "" }
            };
            if (row == null && section == "users") draft["Password"] = "";
            if (row == null && section == "credentials") draft["UserGuid"] = "";
            if (row != null)
            {
                draft["Active"] = true;
                foreach (string key in draft.Select(entry => entry.Key).ToArray()) if (row[key] != null) draft[key] = row[key]!.DeepClone();
            }
            return draft;
        }
        private void AccessEdit(string section, string basePath, JsonNode? row)
        {
            if (!CanAccess(section, true)) return;
            if (row != null && (JsonData.Flag(row, "IsBuiltIn") || JsonData.Flag(row, "IsProtected"))) { _Host.Notify(_Localizer.Phrase("Protected records cannot be changed.")); return; }
            JsonObject draft = AccessDraft(section, row);
            if (section == "users" && JsonData.Flag(_Context, "isAdmin")) draft["IsAdmin"] = row?["IsAdmin"]?.DeepClone() ?? JsonValue.Create(false);
            string path = basePath + (row == null ? "" : "/" + ServerClient.Segment(JsonData.Id(row)));
            if (row == null && section is "credentials" or "assignments")
            {
                Pick(TenantPath + "/users", "Select owner", user =>
                {
                    draft["UserGuid"] = JsonData.Id(user);
                    if (section == "assignments") Pick(TenantPath + "/roles", "Select role", role => { draft["RoleGuid"] = JsonData.Id(role); AccessSaveForm(section, path, "POST", draft); });
                    else AccessSaveForm(section, path, "POST", draft);
                });
                return;
            }
            AccessSaveForm(section, path, row == null ? "POST" : "PUT", draft);
        }
        private void AccessSaveForm(string section, string path, string method, JsonObject draft)
        {
            long revision = _Revision;
            Edit(draft, "Edit " + section, value => Mutate(async ct =>
            {
                if (section == "assignments" && JsonData.Text(value, "ResourceScope") == "Resource" && string.IsNullOrWhiteSpace(JsonData.Text(value, "ResourceGuid"))) throw new ArgumentException("Exact resource ID is required.");
                if (value["ExpiresUtc"]?.ToString() == "") value.Remove("ExpiresUtc");
                JsonNode? result = await _Client.JsonAsync(method, path, value, ct).ConfigureAwait(false);
                Apply(revision, () => { _Form?.MarkClean(); _Form = null; if (section == "credentials" && method == "POST" && result != null) { Track(ShowSecretAsync(result)); } else Access(section); });
            }));
        }
        private void AccessRecord(string section, string basePath, JsonNode row)
        {
            string id = JsonData.Id(row); string prefix = basePath.Split('?')[0] + "/" + ServerClient.Segment(id);
            DefinitionList details = new DefinitionList(); JsonNode safe = JsonData.Redact(row); _CopyText = safe.ToJsonString();
            if (safe is JsonObject obj) foreach (KeyValuePair<string, JsonNode?> item in obj) details.Set(item.Key, SafeText.Clean(item.Value?.ToString()));
            ButtonRow actions = new ButtonRow(); actions.Add(ButtonFor("Back", () => Access(section)));
            bool mutable = CanAccess(section, true) && !JsonData.Flag(row, "IsBuiltIn") && !JsonData.Flag(row, "IsProtected");
            if (mutable && section is not ("audit" or "sessions" or "assignments")) actions.Add(ButtonFor("Edit", () => AccessEdit(section, basePath, row)));
            if (mutable && section != "audit") actions.Add(ButtonFor(section == "sessions" ? "Revoke" : "Delete", () => ConfirmMutation("DELETE", prefix, null, () => Access(section))));
            if (section is "users" or "credentials") actions.Add(ButtonFor("Effective permissions", () => ReadDetails(prefix + "/permissions", "Effective permissions")));
            if (section == "users" && mutable) actions.Add(ButtonFor("Reset password", () => Edit(new JsonObject { ["Password"] = "", ["ConfirmPassword"] = "" }, "Reset password", draft =>
            {
                if (JsonData.Text(draft, "Password") != JsonData.Text(draft, "ConfirmPassword")) { _Host.Notify(_Localizer.Phrase("Passwords do not match.")); return; }
                draft.Remove("ConfirmPassword"); ConfirmMutation("PUT", prefix + "/password", draft, () => { _Form = null; if (id == JsonData.Text(_Context, "userId")) { _Client.Token = null; ClearIdentity(); Track(ConnectAsync()); } else Access(section); });
            })));
            if (section == "roles") actions.Add(ButtonFor("Permission mappings", () => RoleMappings(prefix, mutable)));
            if (section == "credentials")
            {
                actions.Add(ButtonFor("Credential scopes", () => AssignmentList(id)));
                if (mutable) { actions.Add(ButtonFor("Reveal token", () => ConfirmReadSecret(prefix + "/token"))); actions.Add(ButtonFor("Rotate token", () => ConfirmMutation("POST", prefix + "/token/rotate", null, () => ReadDetails(prefix + "/token", "New credential token")))); }
            }
            Show(new FramedStack().Add(new ScrollView(details, 100, Math.Max(30, ((JsonObject)safe).Count * 4)), StackSize.Weighted(1)).Add(actions, StackSize.Fixed(7)), "Access record " + id);
        }
        private void ReadDetails(string path, string title)
        {
            long revision = _Revision; Work(async ct => { JsonNode? result = await _Client.JsonAsync("GET", path, null, ct).ConfigureAwait(false); Apply(revision, () => { if (result != null) { if (path.EndsWith("/token", StringComparison.Ordinal)) Track(ShowSecretAsync(result)); else Inspect(result, title); } }); });
        }
        private async Task ShowSecretAsync(JsonNode result)
        {
            string token = JsonData.Text(result, "Token", "token");
            SecretModal modal = new SecretModal(token, value => Track(CopySecretAsync(value)));
            try { await _Host.ShowAsync(modal).ConfigureAwait(false); }
            finally { _Host.Post(modal.Clear); }
        }
        private async Task CopySecretAsync(string value)
        {
            long revision = _Revision;
            bool copied = await _Clipboard.CopyAsync(value, _Route.Token).ConfigureAwait(false);
            Apply(revision, () => _Host.Notify(copied ? "Copied" : "Clipboard unavailable. Reveal and select the token to copy it."));
        }
        private void ConfirmReadSecret(string path)
        {
            Track(ConfirmReadSecretAsync(path));
        }
        private async Task ConfirmReadSecretAsync(string path)
        {
            if (await _Host.ConfirmAsync("Reveal the retained credential token? This access is audited.").ConfigureAwait(false)) _Host.Post(() => ReadDetails(path, "Credential token | dismiss to clear"));
        }
        private void ConfirmMutation(string method, string path, JsonNode? body, Action completed) { Track(ConfirmMutationAsync(method, path, body, completed)); }
        private async Task ConfirmMutationAsync(string method, string path, JsonNode? body, Action completed)
        {
            long revision = _Revision;
            if (!await _Host.ConfirmAsync(method + " " + path + "?").ConfigureAwait(false)) return;
            _Host.Post(() => Mutate(async ct => { await _Client.JsonAsync(method, path, body, ct).ConfigureAwait(false); Apply(revision, completed); }));
        }
        private void RoleMappings(string rolePath, bool mutable)
        {
            DataTable<JsonNode> table = Table(new[] { "Guid", "Name", "Effect", "ResourceTypes", "OperationTypes", "Active" });
            LoadTable(table, rolePath + "/permissions", row => { Inspect(JsonData.Redact(row), "Permission mapping"); return Task.CompletedTask; });
            ButtonRow actions = new ButtonRow();
            if (mutable)
            {
                actions.Add(ButtonFor("Add mapping", () => Pick(TenantPath + "/permissions", "Select permission", row => ConfirmMutation("POST", rolePath + "/permissions/" + ServerClient.Segment(JsonData.Id(row)), null, () => RoleMappings(rolePath, mutable)))));
                actions.Add(ButtonFor("Remove mapping", () => { if (table.SelectedRow != null) ConfirmMutation("DELETE", rolePath + "/permissions/" + ServerClient.Segment(JsonData.Id(table.SelectedRow)), null, () => RoleMappings(rolePath, mutable)); }));
            }
            Show(new FramedStack().Add(ViewTable(table), StackSize.Weighted(1)).Add(actions, StackSize.Fixed(3)), "Role mappings");
        }
        private void AssignmentList(string credential)
        {
            string path = TenantPath + "/credentials/" + ServerClient.Segment(credential) + "/assignments";
            DataTable<JsonNode> table = Table(AccessColumns("assignments")); LoadTable(table, path);
            ButtonRow actions = new ButtonRow();
            if (CanAccess("assignments", true))
            {
                actions.Add(ButtonFor("Create scope", () => Pick(TenantPath + "/roles", "Select role", row => AccessSaveForm("assignments", path, "POST", new JsonObject { ["RoleGuid"] = JsonData.Id(row), ["ResourceScope"] = "Tenant", ["ResourceGuid"] = "", ["InheritsToChildren"] = true }))));
                actions.Add(ButtonFor("Remove scope", () => { if (table.SelectedRow != null) ConfirmMutation("DELETE", path + "/" + ServerClient.Segment(JsonData.Id(table.SelectedRow)), null, () => AssignmentList(credential)); }));
            }
            Show(new FramedStack().Add(ViewTable(table), StackSize.Weighted(1)).Add(actions, StackSize.Fixed(3)), "Credential scopes");
        }
    }
}
