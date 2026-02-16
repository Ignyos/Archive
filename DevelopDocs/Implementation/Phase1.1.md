# WPF project and core library

## Phase1.1 is done when the following have been achieved.

- Solution/projects created.
  - Archive.Desktop
  - Archive.Core

- Initial folder structure and namespaces aligned to Requirements.

- Basic app settings model + configuration loading approach.
  - appSettings.json is at the root of the code directory which is the /src folder  

- Target framework and template are established.
  - .NET 9
  - WPF App template

- Naming conventions are documented.
  - Namespaces align to the primary namespaces in Requirements.md
  - Folder names mirror project names

- Configuration defaults and overrides are defined.
  - Defaults in appSettings.json

src
|- Archive.Core
|- Archive.Desktop
|- appSettings.json


### Acceptance criteria

- Build succeeds

- App launches

- Config loads