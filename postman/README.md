# Local download simulation

`mocks/huggingface` implements synthetic repository metadata, pinned tree listings, GGUF download bytes,
and HTTP Range/ETag failure cases. It has no real upstream dependency or credentials. Unmodeled paths
return 501. It is intentionally not a general Hugging Face emulator.

```sh
postman simulation run postman/simulations/downloads.sim.yaml --port 4907 --output ndjson
```

In another terminal:

```sh
SHARPAI_TEST_HF_URL=http://127.0.0.1:4907/hf dotnet test src/Test.Xunit -f net8.0
```

Use `scripts/test-operations.sh` to manage the simulation and an isolated real SharpAI server together,
including cleanup and coverage. Details and limitations are in [operations](../docs/OPERATIONS.md).

## Live authentication and token management

The repository collection `../SharpAI.postman_collection.json` uses HTTP bearer authentication. Keep
`bearerToken` in a private environment for normal API access. **Authentication boundary** provides read-only
401/200 assertions for model access and metrics.

**Credential token management** uses `adminBearerToken`, `tenantGuid`, and `credentialGuid` from the environment.
Reveal requires Credential Admin permission, returns a no-store response and is audited. Regeneration immediately
invalidates the old token and preserves its grants/status/expiry; run that request only when replacement is intended.
The replacement is stored in the private environment variable `revealedBearerToken`; do not export it into Git.
Hash-only legacy tokens return 409 from reveal and require regeneration. Ordinary credential reads remain redacted.
The live OpenAPI document is authoritative for routes, permission requirements and response codes. Full details
are in [the deployment guide](../DEPLOYMENT_GUIDE.md#reveal-and-regenerate-api-bearer-tokens).
