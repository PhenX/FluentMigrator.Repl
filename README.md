# FluentMigrator REPL

This tool is based on the code from https://github.com/thatplatypus/BlazorCodeEditor

It is a fully client-side web application built with Blazor WebAssembly that demonstrates a novel approach to dynamically compiling and executing FluentMigrator entirely in the browser.

## Overview
Blazor Code Editor is a minimal, client-side web application built with Blazor WebAssembly. It demonstrates a novel approach to dynamically compiling and executing C# code entirely in the browser. This project is a lightweight public version of Apollo, showcasing its core capabilities in a simplified form.

Features include:
- Client-side compilation and execution of C# code.
- Lightweight, browser-based IDE experience.
- Monaco-based code editor with syntax highlighting and support for C#.
- Support for multiple files within a solution.
- Educational value as an example of dynamic code execution in WebAssembly.

## Prerequisites
- Visual Studio 2022 17.9 or later.
- .NET 9 SDK installed.
- Basic knowledge of C# and Blazor.

## Installation & Build
Clone the repository and build the project using:

```bash
# Clone the repository
git clone https://github.com/thatplatypus/BlazorCodeEditor.git

# Navigate to the project directory
cd BlazorCodeEditor

# Build the project
dotnet build
```

## Setup & Configuration
Blazor Code Editor relies on the following approach to handling WebAssembly with .NET:

- **Webcil Integration**: This project makes use of the [Webcil format](https://github.com/dotnet/runtime/blob/main/docs/design/mono/webcil.md), a WebAssembly-compatible representation of .NET assemblies, enabling C# code to compile and execute dynamically within the browser.
- **Roslyn in WebAssembly**: The project invokes the Roslyn compiler at runtime, resolving metadata references and transpiling assemblies back into a format suitable for execution in the browser.

No server setup is required as the entire application runs within the browser's sandbox.

## Running the Application
To run the application locally:

1. Build the project as shown in the Installation section.
2. Use the following command to launch the application:

```bash
dotnet run --project BlazorCodeEditor
```

3. Open the provided URL in your browser (e.g., `http://localhost:5000`).

## Technical Details
- The application leverages [Mono's Webcil format](https://github.com/dotnet/runtime/blob/main/docs/design/mono/webcil.md) to enable native .NET assembly execution in WebAssembly.
- Metadata references are dynamically resolved at runtime, eliminating the need for pre-bundled DLLs.
- The project adopts a reflection-heavy architecture due to browser runtime constraints, supporting only browser-compatible APIs.

## Educational Use Case
Blazor Code Editor is a perfect starting point for:
- Exploring WebAssembly and Blazor capabilities.
- Understanding dynamic code compilation and execution in a browser environment.
- Building educational tools or demos for teaching C#.

## Contributing
Pull requests are welcome. For major changes, please open an issue to discuss what you would like to change.

## License
This project is licensed under the [MIT License](https://choosealicense.com/licenses/mit/).
