# Entity Framework Migrations Squash CLI Tool

## Overview

The **Migrations Squash CLI Tool** is a helper utility built on top of the official **Entity Framework Core CLI tool**.
Its purpose is to **squash multiple EF Core migrations into a single migration file** when projects accumulate a large migration history.

**Important:** While this tool exists, **squashing migrations is not a recommended practice** in most cases. It should only be considered if you fully understand the risks outlined below.

## Prerequisites

- **.NET 8 SDK** or later
- **Entity Framework Core CLI tool** (`dotnet-ef`) must be installed:
  ```bash
  dotnet tool install --global dotnet-ef
  ```

## Recommended actions before using the tool

* Always **apply all pending migrations** to every environment **before** attempting to squash.
* Prefer keeping the migration history intact - database migrations are lightweight, and EF Core can handle a large number of them.
* Consider squashing **only when** you have an excessively long migration history (hundreds+) and you fully control all environments.

## How the Tool Works

1. **Searches for Initial Migration**
   - The tool inspects your migration files and prompts you to confirm the initial migration file name that will be recreated after squashing.

2. **Generates Backups**
   - Creates a **SQL script** containing all existing migrations for safekeeping (saved in the Migrations folder).
   - Builds an **EF Core migration bundle** (a self-contained executable) containing all migrations. This bundle can later be used to manage and apply the pre-squash migrations.

3. **Deletes Old Migration Files**
   - After backup, all existing migration `.cs` and `Designer.cs` files are deleted.

4. **Re-creates the Initial Migration**
   - Initial migration is overridden, containing the entire schema definition.

## Quick Start

### Install

```bash
dotnet tool install --global DotnetEfMigrationsSquash
```

### Example Usage

```bash
dotnet-ef-migrations-squash --project .\path\to\migrations\project --startup-project .\path\to\startup\project 
```

### Command Line Options

The tool supports all standard Entity Framework Core CLI options including:
- `--project` / `-p`: The project to use
- `--startup-project` / `-s`: The startup project to use
- `--context` / `-c`: The DbContext to use
- `--output-dir` / `-o`: The directory to put files in
- `--configuration`: The configuration to use
- `--framework`: The target framework
- And many more standard EF Core options

### What the Tool Does

* Prompts for the **Initial migration** name confirmation that will be later overridden.
* Generates a **SQL backup script** of all migrations.
* Generates an **EF Core bundle** for safe rollback and migration management.
* Deletes all migration files.
* Recreates the **Initial migration** that now includes all previous migrations.

## Recovery Instructions

If something goes wrong during the squashing process:

1. **Restore from SQL backup**: Use the generated `.sql` file to recreate your database schema
2. **Use the migration bundle**: Run the generated `.exe` bundle file to apply the original migrations
3. **Restore migration files**: If you have source control, revert the changes to restore your original migration files

## Important Notes

* This tool is for **advanced scenarios only**.
* **Do not squash migrations** unless absolutely necessary.
* Always ensure **all environments** (local, staging, production, CI/CD) are fully migrated to the **latest version** before running the tool.
* The `__EFMigrationsHistory` table in your database will not be updated after the new single migration is created. Because the initial migration is overriten.
* Backup files are saved in your project's `Migrations` folder with timestamps.

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request. For major changes, please open an issue first to discuss what you would like to change.

---
