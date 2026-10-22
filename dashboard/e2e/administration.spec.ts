import { test, expect } from "@playwright/test";
import AxeBuilder from "@axe-core/playwright";
const tenantA = {
  Guid: "tenant-a",
  Name: "Alpha",
  Active: true,
  CreatedUtc: "2026-01-01T00:00:00Z",
};
const tenantB = {
  Guid: "tenant-b",
  Name: "Beta",
  Active: true,
  CreatedUtc: "2026-01-01T00:00:00Z",
};
const user = {
  Guid: "user-a",
  TenantGuid: "tenant-a",
  Email: "owner@example.test",
  FirstName: "Owner",
  LastName: "Alpha",
  Active: true,
  CreatedUtc: "2026-01-01T00:00:00Z",
};
const role = {
  Guid: "role-a",
  TenantGuid: "tenant-a",
  Name: "Model viewer",
  Active: true,
  CreatedUtc: "2026-01-01T00:00:00Z",
};
const permission = {
  Guid: "permission-a",
  TenantGuid: "tenant-a",
  Name: "Read models",
  ResourceTypes: ["Model"],
  OperationTypes: ["Read"],
  Effect: "Permit",
  Active: true,
  CreatedUtc: "2026-01-01T00:00:00Z",
};
const capabilityNames = [
  "Tenant",
  "User",
  "Credential",
  "Role",
  "Permission",
  "Assignment",
  "Session",
  "Audit",
];
const context = {
  authEnabled: false,
  isAdmin: true,
  isTenantAdmin: false,
  tenantId: null,
  userId: null,
  capabilities: Object.fromEntries(
    capabilityNames.map((name) => [name, { read: true, write: true }]),
  ),
};
const pageOf = (Objects: unknown[]) => ({
  Objects,
  TotalRecords: Objects.length,
  EndOfResults: true,
});
test.beforeEach(async ({ page }) => {
  await page.route("**/*", async (route) => {
    const req = route.request();
    const path = new URL(req.url()).pathname;
    if (path === "/v1.0/auth/context") return route.fulfill({ json: context });
    if (path === "/health")
      return route.fulfill({ json: { status: "healthy" } });
    if (!path.startsWith("/v1.0/")) return route.continue();
    if (path === "/v1.0/tenants")
      return route.fulfill({ json: pageOf([tenantA, tenantB]) });
    if (path === "/v1.0/tenants/tenant-a/users")
      return route.fulfill({ json: pageOf([user]) });
    if (path === "/v1.0/tenants/tenant-b/users")
      return route.fulfill({
        json: pageOf([
          {
            ...user,
            Guid: "user-b",
            TenantGuid: "tenant-b",
            Email: "beta@example.test",
          },
        ]),
      });
    if (path.endsWith("/roles/role-a/permissions"))
      return route.fulfill({ json: [permission] });
    if (path.endsWith("/roles"))
      return route.fulfill({
        json: pageOf([
          role,
          {
            ...role,
            Guid: "builtin-role",
            TenantGuid: null,
            Name: "Viewer",
            IsBuiltIn: true,
            IsProtected: true,
          },
        ]),
      });
    if (path.endsWith("/permissions"))
      return route.fulfill({ json: pageOf([permission]) });
    if (path.endsWith("/credentials"))
      return route.fulfill({
        json: pageOf([
          {
            Guid: "cred-a",
            TenantGuid: "tenant-a",
            UserGuid: "user-a",
            Name: "CI",
            AccessKey: "access_preview",
            Active: true,
          },
        ]),
      });
    if (path.endsWith("/sessions"))
      return route.fulfill({
        json: pageOf([
          {
            Guid: "session-a",
            UserGuid: "user-a",
            Active: true,
            ExpiresUtc: "2099-01-01T00:00:00Z",
          },
        ]),
      });
    if (path.endsWith("/assignments"))
      return route.fulfill({
        json: pageOf([
          {
            Guid: "assignment-a",
            RoleGuid: "role-a",
            ResourceScope: "Tenant",
            InheritsToChildren: true,
            Active: true,
          },
        ]),
      });
    if (path === "/v1.0/api/audit")
      return route.fulfill({
        json: pageOf([
          {
            Guid: "audit-a",
            EventType: "PasswordReset",
            AuthzResult: true,
            CreatedUtc: "2026-01-01T00:00:00Z",
          },
        ]),
      });
    return route.continue();
  });
});
for (const {path, title} of [
  {path:"tenants",title:"Tenants"},
  {path:"users",title:"Users"},
  {path:"credentials",title:"Credentials"},
  {path:"roles",title:"Roles"},
  {path:"permissions",title:"Permissions"},
  {path:"assignments",title:"Assignments"},
  {path:"sessions",title:"Sessions"},
  {path:"audit",title:"Security audit"},
]) {
  test(`${title} administration renders and remains accessible`, async ({
    page,
  }) => {
    await page.goto("/" + path);
    await expect(
      page.getByRole("heading", { level: 1, name: title, exact: true }),
    ).toBeVisible();
    await expect(
      page.getByRole("combobox", { name: "Selected tenant" }),
    ).toHaveValue("tenant-a");
    await expect(page.locator("table")).toBeVisible();
    const violations = (
      await new AxeBuilder({ page }).analyze()
    ).violations.filter((v) =>
      ["critical", "serious"].includes(v.impact ?? ""),
    );
    expect(violations).toEqual([]);
    expect(
      await page.evaluate(
        () =>
          document.documentElement.scrollWidth -
          document.documentElement.clientWidth,
      ),
    ).toBeLessThanOrEqual(1);
    await page.screenshot({
      path: `test-results/screenshots/${test.info().project.name}-admin-${path}.png`,
    });
  });
}
test("tenant selection clears the previous tenant records and scopes subsequent requests", async ({
  page,
}) => {
  await page.goto("/users");
  await expect(
    page.getByText("owner@example.test", { exact: true }),
  ).toBeVisible();
  await page
    .getByRole("combobox", { name: "Selected tenant" })
    .selectOption("tenant-b");
  await expect(
    page.getByText("beta@example.test", { exact: true }),
  ).toBeVisible();
  await expect(
    page.getByText("owner@example.test", { exact: true }),
  ).toHaveCount(0);
  const toggle=page.getByRole("button",{name:"Toggle navigation"});
  if(await toggle.isVisible()) { await toggle.click(); await toggle.click(); }
  await page.getByRole("link", { name: "Credentials", exact: true }).click();
  await expect(
    page.getByRole("combobox", { name: "Selected tenant" }),
  ).toHaveValue("tenant-b");
});
test("user creation submits a typed form and does not include immutable fields", async ({
  page,
}) => {
  let submitted: Record<string, unknown> | undefined;
  await page.route("**/v1.0/tenants/tenant-a/users", async (route) => {
    if (route.request().method() === "POST") {
      submitted = route.request().postDataJSON();
      await route.fulfill({ json: { ...user, Email: submitted!.Email } });
    } else await route.fulfill({ json: pageOf([user]) });
  });
  await page.goto("/users");
  await page.getByRole("button", { name: "Add", exact: true }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Email", { exact: true }).fill("new@example.test");
  await dialog.getByLabel("Password", { exact: true }).fill("A test password");
  await dialog
    .getByLabel("Confirm password", { exact: true })
    .fill("A test password");
  await dialog.getByRole("button", { name: "Save", exact: true }).click();
  await expect(dialog).toHaveCount(0);
  expect(submitted).toMatchObject({
    Email: "new@example.test",
    Password: "A test password",
    IsAdmin: false,
    IsTenantAdmin: false,
  });
  expect(submitted).not.toHaveProperty("Guid");
  expect(submitted).not.toHaveProperty("TenantGuid");
});
test("one-time credential secret disappears after closing its result dialog", async ({
  page,
}) => {
  await page.route("**/v1.0/tenants/tenant-a/credentials", (route) =>
    route.fulfill({
      json:
        route.request().method() === "POST"
          ? {
              guid: "new-credential",
              accessKey: "access_once",
              secretKey: "only-visible-once",
            }
          : pageOf([]),
    }),
  );
  await page.goto("/credentials");
  await page.getByRole("button", { name: "Add", exact: true }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Name", { exact: true }).fill("Integration");
  await expect(dialog.getByLabel("Owning user", { exact: true })).toHaveValue(
    "user-a",
  );
  await dialog.getByRole("button", { name: "Save", exact: true }).click();
  await expect(
    page.getByText("only-visible-once", { exact: true }),
  ).toBeVisible();
  await page.getByRole("button", { name: "I have saved the secret" }).click();
  await expect(
    page.getByText("only-visible-once", { exact: true }),
  ).toHaveCount(0);
});
test("custom permissions use resource and operation checkboxes", async ({
  page,
}) => {
  let submitted: unknown;
  await page.route("**/v1.0/tenants/tenant-a/permissions", (route) => {
    if (route.request().method() === "POST")
      submitted = route.request().postDataJSON();
    return route.fulfill({
      json:
        route.request().method() === "POST" ? permission : pageOf([permission]),
    });
  });
  await page.goto("/permissions");
  await page.getByRole("button", { name: "Add", exact: true }).click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Name", { exact: true }).fill("Block model changes");
  await dialog.getByLabel("Models", { exact: true }).check();
  await dialog.getByLabel("Delete", { exact: true }).check();
  await dialog.getByLabel("Effect", { exact: true }).selectOption("Deny");
  await dialog.getByRole("button", { name: "Save", exact: true }).click();
  await expect(dialog).toHaveCount(0);
  expect(submitted).toMatchObject({
    Name: "Block model changes",
    ResourceTypes: ["Model"],
    OperationTypes: ["Delete"],
    Effect: "Deny",
  });
});
test("read-only users cannot see mutation controls or platform tenant navigation", async ({
  page,
}) => {
  await page.route("**/v1.0/auth/context", (route) =>
    route.fulfill({
      json: {
        ...context,
        isAdmin: false,
        authEnabled: true,
        tenantId: "tenant-a",
        tenantName: "Alpha",
        capabilities: Object.fromEntries(
          capabilityNames.map((name) => [
            name,
            { read: name === "User", write: false },
          ]),
        ),
      },
    }),
  );
  await page.goto("/users");
  await expect(
    page.getByText("owner@example.test", { exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Add", exact: true }),
  ).toHaveCount(0);
  await expect(
    page.getByRole("link", { name: "Tenants", exact: true }),
  ).toHaveCount(0);
  await expect(
    page.getByRole("combobox", { name: "Selected tenant" }),
  ).toBeDisabled();
  await page.getByRole("button", { name: "Actions", exact: true }).click();
  await expect(
    page.getByRole("menuitem", { name: "Edit", exact: true }),
  ).toHaveCount(0);
});
test("protected roles cannot be edited or deleted", async ({ page }) => {
  await page.goto("/roles");
  const row = page
    .getByRole("row")
    .filter({ hasText: "Viewer" })
    .filter({ hasText: "Built-in" });
  await row.getByRole("button", { name: "Actions" }).click();
  await expect(
    page.getByRole("menuitem", { name: "Edit", exact: true }),
  ).toBeDisabled();
  await expect(
    page.getByRole("menuitem", { name: "Delete", exact: true }),
  ).toBeDisabled();
});
test("password reset requires confirmation and sends no account metadata", async ({
  page,
}) => {
  let submitted: unknown;
  await page.route("**/users/user-a/password", (route) => {
    submitted = route.request().postDataJSON();
    return route.fulfill({ json: { updated: true } });
  });
  await page.goto("/users");
  await page.getByRole("button", { name: "Actions" }).click();
  await page
    .getByRole("menuitem", { name: "Reset password", exact: true })
    .click();
  const dialog = page.getByRole("dialog");
  await dialog.getByLabel("Password", { exact: true }).fill("Replacement");
  await dialog
    .getByLabel("Confirm password", { exact: true })
    .fill("Different");
  await dialog.getByRole("button", { name: "Save" }).click();
  await expect(dialog.getByRole("alert")).toContainText("do not match");
  expect(submitted).toBeUndefined();
  await dialog
    .getByLabel("Confirm password", { exact: true })
    .fill("Replacement");
  await dialog.getByRole("button", { name: "Save" }).click();
  await expect(dialog).toHaveCount(0);
  expect(submitted).toEqual({ Password: "Replacement" });
});

test('late responses from a previous tenant never replace the current tenant records',async({page})=>{
  let calls=0;let release:()=>void=()=>{};
  await page.route('**/v1.0/tenants/tenant-a/users?*',async route=>{
    calls++;if(calls>1)await new Promise<void>(resolve=>{release=resolve;});
    await route.fulfill({json:pageOf([user])});
  });
  await page.goto('/users');await expect(page.getByText('owner@example.test',{exact:true})).toBeVisible();
  await page.getByRole('button',{name:'Refresh',exact:true}).click();await expect.poll(()=>calls).toBe(2);
  await page.getByRole('combobox',{name:'Selected tenant'}).selectOption('tenant-b');
  await expect(page.getByText('beta@example.test',{exact:true})).toBeVisible();
  const completed=page.waitForResponse(response=>response.url().includes('/tenant-a/users'));
  release();await completed;
  await expect(page.getByText('owner@example.test',{exact:true})).toHaveCount(0);
  await expect(page.getByText('beta@example.test',{exact:true})).toBeVisible();
});

test('credential role grants use the selected principal and exact resource scope',async({page})=>{
  let submitted:unknown;
  await page.route('**/credentials/cred-a/assignments',route=>{if(route.request().method()==='POST')submitted=route.request().postDataJSON();return route.fulfill({json:route.request().method()==='POST'?{Guid:'new-assignment'}:[]});});
  await page.goto('/assignments');await page.getByRole('combobox',{name:'Principal type',exact:true}).selectOption('credentials');
  await expect(page.getByRole('combobox',{name:'Principal',exact:true})).toHaveValue('cred-a');
  await page.getByRole('button',{name:'Grant role',exact:true}).click();const dialog=page.getByRole('dialog');
  await expect(dialog.getByLabel('Role',{exact:true})).toHaveValue('role-a');
  await dialog.getByLabel('Scope',{exact:true}).selectOption('Resource');await dialog.getByLabel('Resource ID',{exact:true}).fill('model-resource-id');
  await dialog.getByRole('button',{name:'Save',exact:true}).click();await expect(dialog).toHaveCount(0);
  expect(submitted).toMatchObject({RoleGuid:'role-a',ResourceScope:'Resource',ResourceGuid:'model-resource-id',InheritsToChildren:false});
  expect(submitted).not.toHaveProperty('TenantGuid');expect(submitted).not.toHaveProperty('UserGuid');
});

test('custom role permission mappings can be added and removed',async({page})=>{
  const extra={...permission,Guid:'permission-extra',Name:'Read audit'};let mapped=false;
  await page.route('**/tenant-a/permissions?*',route=>route.fulfill({json:pageOf([permission,extra])}));
  await page.route('**/roles/role-a/permissions',route=>route.fulfill({json:mapped?[permission,extra]:[permission]}));
  await page.route('**/roles/role-a/permissions/permission-extra',route=>{mapped=route.request().method()==='POST';return route.fulfill({json:{deleted:!mapped}});});
  await page.goto('/roles');await page.getByRole('row').filter({hasText:'Model viewer'}).getByRole('button',{name:'Actions'}).click();await page.getByRole('menuitem',{name:'Role permissions',exact:true}).click();
  const dialog=page.getByRole('dialog');await dialog.getByRole('combobox',{name:'Permission',exact:true}).selectOption('permission-extra');await dialog.getByRole('button',{name:'Add permission',exact:true}).click();
  await expect(dialog.getByRole('row').filter({hasText:'Read audit'})).toBeVisible();
  await dialog.getByRole('row').filter({hasText:'Read audit'}).getByRole('button',{name:'Remove mapping'}).click();
  await expect(dialog.getByRole('row').filter({hasText:'Read audit'})).toHaveCount(0);
});

test('Access consolidates navigation and tabs preserve tenant scope',async({page})=>{
  await page.goto('/access/users');
  await expect(page.getByRole('heading',{level:1,name:'Users',exact:true})).toBeVisible();
  await expect(page.locator('.sa-sidebar a[href="/access"]')).toHaveCount(1);
  await expect(page.locator('.sa-sidebar a[href="/users"]')).toHaveCount(0);
  await page.getByRole('combobox',{name:'Selected tenant'}).selectOption('tenant-b');
  await page.locator('.sa-access-tabs').getByRole('link',{name:'Credentials',exact:true}).click();
  await expect(page).toHaveURL(/\/access\/credentials$/);
  await expect(page.getByRole('combobox',{name:'Selected tenant'})).toHaveValue('tenant-b');
  await page.goBack();
  await expect(page).toHaveURL(/\/access\/users$/);
});
