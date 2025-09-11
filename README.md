# Migrations Squash CLI Tool for EF Core

## Overview

The **Migrations Squash CLI Tool** is a helper utility built on top of the official **Entity Framework Core CLI tool**.
Its purpose is to **squash multiple EF Core migrations into a single migration file** when projects accumulate a large migration history.

**Important:** While this tool exists, **squashing migrations is not a recommended practice** in most cases. It should only be considered if you fully understand the risks outlined below.

## Recommended actions before using the  tool

* Always **apply all pending migrations** to every environment **before** attempting to squash.
* Prefer keeping the migration history intact - database migrations are lightweight, and EF Core can handle a large number of them.
* Consider squashing **only when** you have an excessively long migration history (hundreds+) and you fully control all environments.

## How the Tool Works

1. **Searches for Initial Migration**
   - The tool inspects your migration files and prompts you to confirm the initial migration file name that will be recreated after squashing.

2. **Generates Backups**

   - Creates a **SQL script** containing all existing migrations for safekeeping.
   - Builds an **EF Core migration bundle** (a self-contained executable) containing all migrations. This bundle can later be used to manage and apply the pre-squash migrations.

3. **Deletes Old Migration Files**
   - After backup, all existing migration `.cs` and `Designer.cs` files are deleted.

4. **Re-creates the Initial Migration**
   - Initial migration is overriden, containing the entire schema definition.

## Quick Start

### Install

```bash
dotnet tool install --global dotnet-ef-migrations-squash
```

### Example Workflow

```bash
dotnet-ef-migrations-squash --project .\path\to\migrations\project --startup-project .\path\to\startup\project 
```

* Prompts for the **Initial igration** name confirmation that will be later overrriden.
* Generates a **SQL backup script** of all migrations.
* Generates an **EF Core bundle** for safe rollback and migration management.
* Deletes all migration files.
* Recreates the **Initial migration** that now includes all previous migrations.

## Notes

* This tool is for **advanced scenarios only**.
* **Do not squash migrations** unless absolutely necessary.
* Always ensure **all environments** (local, staging, production, CI/CD) are fully migrated to the **latest version** before running the tool.

---
