## Tools

### Diff Export

**.ai\scripts\export_diffs_and_content.ps1**: Get all changes made relative to the base branch. Run this script from the repo root and review the generated files under `.ai/Temp/pr/` when continuing work on a non-master branch and work item requires prior change analysis.

Outputs:

    - `.ai/Temp/pr/all-diffs.txt`: consolidated diffs.
    - `.ai/Temp/pr/all-pre-content.txt`: consolidated pre-content.
    - `.ai/Temp/pr/all-post-content.txt`: consolidated post-content.

## PowerShell Script Validation After Modification

- After modifying `.ps1` files, use the preconfigured validation script:
   - **Validate a specific pattern:** `.\.ai\scripts\validate-scripts-powershell.ps1 -FilePattern "Script_*.ps1"`
   - **Validate a single file:** `.\.ai\scripts\validate-scripts-powershell.ps1 -FilePattern "Setup_Script_1.ps1"`
- Review the output for any warnings or errors.
- Address the identified issues.

## Environment

Terminal sessions use PowerShell by default; always invoke scripts directly from the repository root.

### Forbidden PowerShell Host Prefixes

Do not prefix any command or script execution with `pwsh`, `powershell`, or `powershell.exe`, and do not use host-wrapper flags such as `-Command`, `-File`, `-NoProfile`, or `-ExecutionPolicy`. Commands must be provided as native PowerShell statements, and scripts must be invoked directly.

Examples:

```powershell
# WRONG (host prefix + host wrapper flags)
pwsh -NoProfile -ExecutionPolicy Bypass -File .\.ai\scripts\Start-Local.ps1 MONITOR
powershell -Command "Get-ChildItem"

# VALID (direct invocation)
.\.ai\scripts\Start-Local.ps1 MONITOR

# VALID (native PowerShell statement)
Get-ChildItem -Path . -Recurse -Filter *.csproj -File
```
