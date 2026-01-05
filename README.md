# Archive

A fast, reliable directory synchronization and backup utility for Windows.

**[📥 Download](https://github.com/Ignyos/Archive/releases) | [📖 Documentation](https://ignyos.github.io/archive/) | [🐛 Report Issues](https://github.com/Ignyos/Archive/issues)**

## Overview

Archive provides automated file synchronization and backup with scheduling, detailed logging, and an intuitive interface. Available as both a GUI application and command-line tool.

### Key Features

- Fast incremental sync - only copies changed files
- Flexible scheduling with time windows
- Detailed operation history and logging
- Preview operations before running (dry-run mode)
- Optional hash verification
- System tray integration

## For Users

For installation instructions, user guides, and documentation:

- **[GUI Documentation](https://ignyos.github.io/archive/docs.html)** - Graphical interface guide
- **[Portable Version](https://ignyos.github.io/archive/portable.html)** - No-install option
- **[Console Documentation](https://ignyos.github.io/archive/console.html)** - Command-line usage

## For Developers

### Prerequisites
- Windows 10/11 (for GUI), or any platform supporting .NET (for console)

### Building from Source

```powershell
git clone https://github.com/Ignyos/Archive.git
cd Archive
dotnet restore
dotnet build
```

### Running Locally

**Console Application:**
```powershell
cd Archive.Console
dotnet run -- sync --source "C:\Source" --destination "D:\Dest"
```

**GUI Application:**
```powershell
cd Archive.GUI
dotnet run
```

### Publishing

**Console (self-contained):**
```powershell
cd Archive.Console
dotnet publish -c Release -r win-x64 --self-contained
```

**GUI (self-contained):**
```powershell
cd Archive.GUI
dotnet publish -c Release -r win-x64 --self-contained
```

Output will be in `bin/Release/net9.0-windows/win-x64/publish/`

### Creating Releases

The project uses GitHub Actions for automated releases. To create a new release:

1. Update version in `setup.iss` (for installer)
2. Commit changes
3. Create and push a tag:
   ```powershell
   git tag v1.0.0
   git push origin v1.0.0
   ```
4. GitHub Actions will automatically:
   - Build both console and GUI applications
   - Create installer using InnoSetup
   - Create GitHub release
   - Upload all artifacts

### Project Structure

```
Archive/
├── Archive.Core/          # Core synchronization engine
│   ├── SyncEngine.cs      # Main sync logic
│   ├── BackupJob.cs       # Job configuration
│   └── SyncOptions.cs     # Sync operation options
├── Archive.Console/       # Command-line interface
│   └── Program.cs         # CLI entry point
├── Archive.GUI/           # WPF GUI application
│   ├── MainWindow.xaml    # Main interface
│   ├── Services/          # Background services
│   └── Windows/           # Dialog windows
├── Archive.Pages/         # GitHub Pages documentation site
└── .github/workflows/     # CI/CD automation
```

### Key Components

**Archive.Core** - Platform-agnostic synchronization library
- File comparison and delta detection
- Hash-based verification (SHA256)
- Exclusion pattern matching
- Dry-run/preview mode
- Progress reporting

**Archive.GUI** - Windows desktop application
- WPF-based interface
- SQLite database for job storage
- System tray integration
- Scheduler service for automated runs
- Update checker via GitHub API

**Archive.Console** - Command-line tool
- Direct sync operations
- Config file-based job management
- Scriptable for automation
- Works with Windows Task Scheduler

## Roadmap (considerations for new features)
- Advanced scheduling for less/more periodic backup scenarios
- Bandwidth throttling for large transfers
- Cloud storage integration (OneDrive, Dropbox, etc.)

## License

This project is licensed under the MIT License. You are free to use, modify, and distribute this software, including for commercial purposes.

## Support

- **Documentation**: https://ignyos.github.io/archive/
- **Issues**: https://github.com/Ignyos/Archive/issues
- **Discussions**: https://github.com/Ignyos/Archive/discussions


