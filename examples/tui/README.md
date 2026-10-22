# Remote read-only walkthrough

Copy `remote-profile.json` to a private directory as `tui.json`, replace the example endpoints, then run:

```sh
sharpai-tui --profile-directory /absolute/path/to/profile
```

Use Commands (`Ctrl+P`) to sign in with the tenant, email and password supplied by your administrator, or enter an existing bearer credential. The client retains entered secrets in memory. An administrator can give the identity read permissions without mutation permissions; authorization is enforced by the server on every request.

Open Models to inspect registered and resident models. Open Request History, choose a UTC range, select an activity interval, and press Enter on a request to inspect its metadata and bodies. Missing, truncated and withheld bodies remain explicit. On a narrow terminal the detail tabs keep each pane accessible. Copy selected IDs from Commands; select columns and cycle page size there as well. The filter above each table applies only to the loaded page.

Open Observability to distinguish server health/readiness and Watson HTTP metrics from an independently configured engine collector. Copy a configured service URL to inspect it in your browser. `F1` shows focused help, `F2` opens navigation, and `Ctrl+Q` quits and restores the terminal. Delete the example profile directory to remove its nonsecret preferences and lifecycle logs. The app does not delete operator-owned token files.

Do not use API Explorer mutations in a read-only walkthrough. A read-only credential is still required to enforce that boundary independently of the UI.
