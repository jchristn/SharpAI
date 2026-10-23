# SharpAI.Serve

Serve one local GGUF model with Ollama-compatible and OpenAI-compatible inference APIs:

```sh
sharpai serve /path/to/model.gguf --port 8000
```

This standalone executable has no authentication, dashboard, management endpoints, registry, or
configuration database. It loads the model before accepting requests. Models remain at the paths you
provide. See [standalone guide](https://github.com/jchristn/SharpAI/blob/main/docs/SERVE.md) for installation, options, examples, and validation.

The NuGet tool package is `SharpAI.Serve`; the installed command is `sharpai`.

```sh
dotnet tool install --global SharpAI.Serve
dotnet tool update --global SharpAI.Serve
dotnet tool uninstall --global SharpAI.Serve
```

Global commands require the .NET tool directory on PATH and a compatible .NET runtime. A local package
can be packed and installed before public publication; see the repository guide. Package availability
on NuGet depends on release publication. Relative model/template/log paths resolve from the caller's
working directory, so the installed command can run anywhere.

CPU and macOS Metal native libraries are bundled. CUDA requires an explicit matching external
native library through `--backend cuda --native-library <path>`. See the guide for prerequisites.

Startup prints the SharpAI ASCII banner. Debug REST logs are enabled by default and report method,
path, source, bytes, status, duration, and available TTFT/token counts for both response modes.
Use `--log-level info` to hide request logs, `--quiet` to suppress application console logs,
and `--log-file <path>` for optional file output. Prompts, responses, and headers are not logged.

For complete compact JSON records, use `--log-format json --log-detail detailed`. Detail levels are
`off`, `minimal`, `standard` (default), and `detailed`. Detailed records include engine queue/load/prompt/
generation phases, throughput, timing provenance, cancellation, stream payload counts, runtime configuration,
and request/trace IDs. Unavailable values are null. Stdout and the optional file contain NDJSON;
native backend stderr can remain unstructured. See the standalone guide for the field schema.
