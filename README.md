# JagPav.Pipeline

A small, dependency-free compensable pipeline. It exists to **simplify orchestration logic**: instead of hand-rolling try/catch blocks and manual rollback code for a multi-step process, you declare the steps once and the pipeline runs them in order, compensating (rolling back) whatever already succeeded if a later step fails.

Every step shares one contract — the shared model plus a cancellation primitive — so a pipeline definition reads as a flat list of step references instead of bespoke glue code.

## Implementations

| Folder | Package | Docs |
| --- | --- | --- |
| [`csharp/`](csharp) | NuGet: `JagPav.Pipeline` | [csharp/README.md](csharp/README.md) |
| [`ts/`](ts) | npm: `@jagpav/pipeline` | [ts/README.md](ts/README.md) |

## Repository layout

```
csharp/   .NET solution (JagPavPipeline.sln): library, unit tests, sample API, functional tests
ts/       TypeScript package: library and unit tests
```

## License

[MIT](LICENSE)
