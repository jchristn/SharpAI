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
