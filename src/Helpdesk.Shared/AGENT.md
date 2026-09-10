# Helpdesk.Shared Agent Instructions

## 1. Project Purpose
The `Helpdesk.Shared` project contains only code and contracts that are shared between the `Helpdesk.API` and `Helpdesk.NewWeb` projects.

## 2. Allowed Content
- **Data Transfer Objects (DTOs):** Plain C# `record` or `class` types used for API requests and responses. They are placed under the `Helpdesk.Shared.DTOs` namespace.
- **Enums and Constants:** Shared enumerations or constant definitions.

## 3. Strict Prohibitions
- **DO NOT** add references to UI libraries such as `MudBlazor`.
- **DO NOT** add references to data-access libraries like `EntityFrameworkCore`.
- **DO NOT** implement business logic, services, or UI components in this project.
- **DO NOT** add dependencies on any other project in the solution. This project must have zero project dependencies.
