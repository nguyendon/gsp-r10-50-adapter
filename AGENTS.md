# Repository Guidelines

## Project Structure & Module Organization
The application is a single .NET project rooted at `gspro-r10.csproj`. Runtime code lives in `src/`: `api/` contains protocol adapters, `connections/` holds network and device transport code, `bluetooth/` contains device and protobuf integration, and `util/` holds shared helpers such as logging, CRC, and byte utilities. Build helpers live in `build/`, while repository-level assets include `icon.ico`, `screenshot.png`, and the user-facing `settings.json`.

## Build, Test, and Development Commands
- `dotnet restore` installs NuGet dependencies from `Nuget.Config`.
- `dotnet build` compiles the Windows-targeted .NET 7 executable.
- `dotnet run` starts the adapter locally using the `settings.json` in the working directory.
- `bash build/publish-win64-dotnet.sh` publishes a framework-dependent Windows x64 single-file build.
- `bash build/publish-win64-selfcontained.sh` creates self-contained Windows x64 release zips in `publish/`.

Run commands from the repository root so `settings.json` is discovered correctly.

## Coding Style & Naming Conventions
Follow the existing C# style in `src/`: two-space indentation, braces on their own lines, and concise methods. Use PascalCase for classes, methods, enums, and properties; keep private fields consistent with existing code (`OpenConnectClient`, `disposedValue`). Keep namespaces under `gspro_r10`, and place new files in the feature folder that matches the transport or protocol they extend. No formatter or linter is checked in, so keep edits consistent with nearby files.

## Testing Guidelines
There is currently no dedicated test project in this repository. For changes, at minimum run `dotnet build` and smoke-test with `dotnet run` against a local `settings.json` scenario relevant to your feature, especially for Bluetooth, OpenConnect, or putting flows. If you add tests, place them in a separate `tests/` project and use names like `ConnectionManagerTests.cs`.

## Commit & Pull Request Guidelines
Recent commits use short, imperative subjects in lowercase, sometimes with a scope prefix, for example `fix putting app opening twice (#34)` or `hotfix: replace unicode box characters with ascii...`. Keep commits focused and reference the related issue or PR number when available. Pull requests should describe the user-visible behavior change, list any `settings.json` impacts, and include logs or screenshots when protocol output or runtime behavior changes.

## Configuration & Release Notes
Treat `settings.json` as local runtime configuration. Avoid committing machine-specific values unless they are intentional defaults. The self-contained publish script rewrites the Bluetooth default before packaging, so review generated artifacts before release.
