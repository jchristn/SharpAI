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
