# Script: Sync-AgentAssets.ps1
# Location: .ai/skills/ai-self-improvement/scripts/Sync-AgentAssets.ps1
# Description:
#   Synchronises AI agent instruction files, skills, and custom agents from master sources under `.ai/`.
#   Agent definitions are loaded from `agents.json` (next to this script's parent folder).
#
# Options for Mode:
#   ALL  - update all known agent outputs
#   AUTO - update only agents that exist in this repository (default usage)
#   Or a specific agent name (e.g. "Claude Code", "Roo Code")
#
# Options:
#   -Global  - also sync global agents from .ai/.global/agents/ to user-level paths (off by default)
#   -NoClear - do not clear the console on start

param(
    [Parameter(Position = 0)]
    [string]$Mode,

    [switch]$Global,

    [switch]$NoClear
)

# Combine remaining args so invocations like:
#   Sync-AgentAssets.ps1 GitHub CoPilot
# work the same as:
#   Sync-AgentAssets.ps1 "GitHub CoPilot"
if ($args.Count -gt 0) {
    $ModeFromArgs = ($args -join ' ')
    if (-not $Mode -or $Mode -eq '') {
        $Mode = $ModeFromArgs
    }
}

$scriptName = [System.IO.Path]::GetFileName($MyInvocation.MyCommand.Path)

# Strict mode
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ── Paths ────────────────────────────────────────────────────────────────────

$scriptDir = $PSScriptRoot
$skillDir = (Join-Path -Path $scriptDir -ChildPath ".." | Resolve-Path).Path
$repoRoot = (Join-Path -Path $scriptDir -ChildPath "..\..\..\.." | Resolve-Path).Path
$aiDir = Join-Path $repoRoot ".ai"

# ── Load agent config ────────────────────────────────────────────────────────

$configPath = Join-Path $skillDir "agents.json"
if (-not (Test-Path $configPath -PathType Leaf)) {
    throw "Agent configuration not found: $configPath"
}
$config = Get-Content -Path $configPath -Raw | ConvertFrom-Json

# ── Utility functions ────────────────────────────────────────────────────────

function Ensure-Directory {
    param([Parameter(Mandatory)] [string]$Path)
    if (-not (Test-Path -Path $Path -PathType Container)) {
        New-Item -ItemType Directory -Force -Path $Path | Out-Null
    }
}

function Test-HasInstructionFiles {
    param(
        [Parameter(Mandatory)] [string]$Path,
        [string]$Filter = '*instructions.md'
    )
    if (Test-Path $Path -PathType Container) {
        $files = @(Get-ChildItem $Path -Filter $Filter -File -ErrorAction SilentlyContinue)
        return ($files.Length -gt 0)
    }
    return $false
}

function Copy-FileIfDifferent {
    param(
        [Parameter(Mandatory)] [string]$SourcePath,
        [Parameter(Mandatory)] [string]$TargetPath
    )

    $targetDir = Split-Path -Path $TargetPath -Parent
    Ensure-Directory -Path $targetDir

    if (-not (Test-Path -Path $TargetPath -PathType Leaf)) {
        Copy-Item -LiteralPath $SourcePath -Destination $TargetPath -Force
        Write-Host "Created: $TargetPath"
        return
    }

    $srcBytes = [System.IO.File]::ReadAllBytes($SourcePath)
    $dstBytes = [System.IO.File]::ReadAllBytes($TargetPath)

    if ($srcBytes.Length -eq $dstBytes.Length) {
        $same = $true
        for ($i = 0; $i -lt $srcBytes.Length; $i++) {
            if ($srcBytes[$i] -ne $dstBytes[$i]) { $same = $false; break }
        }
        if ($same) {
            Write-Host "Up-to-date: $TargetPath"
            return
        }
    }

    Copy-Item -LiteralPath $SourcePath -Destination $TargetPath -Force
    Write-Host "Updated: $TargetPath"
}

function Get-TextAuto {
    param([Parameter(Mandatory)] [string]$Path)
    $sr = New-Object System.IO.StreamReader($Path, $true)
    try { return $sr.ReadToEnd() }
    finally { $sr.Dispose() }
}

function Write-Utf8NoBom {
    param(
        [Parameter(Mandatory)] [string]$Path,
        [Parameter(Mandatory)] [string]$Content
    )
    $dir = Split-Path -Path $Path -Parent
    Ensure-Directory -Path $dir
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $utf8NoBom)
}

function Assert-InstructionSync {
    param(
        [Parameter(Mandatory)] [string]$SourceDirectory,
        [Parameter(Mandatory)] [string]$TargetDirectory,
        [Parameter(Mandatory)] [System.IO.FileSystemInfo[]]$SourceFiles
    )

    $srcDir = Join-Path $repoRoot $SourceDirectory
    $dstDir = Join-Path $repoRoot $TargetDirectory

    foreach ($sourceFile in $SourceFiles) {
        $srcPath = Join-Path $srcDir $sourceFile.Name
        $dstPath = Join-Path $dstDir $sourceFile.Name

        if (-not (Test-Path $dstPath -PathType Leaf)) {
            throw "Binary comparison failed. Destination file missing: $dstPath"
        }

        $srcBytes = [System.IO.File]::ReadAllBytes($srcPath)
        $dstBytes = [System.IO.File]::ReadAllBytes($dstPath)

        if ($srcBytes.Length -ne $dstBytes.Length) {
            throw "Binary comparison failed. Size mismatch: Source: $srcPath Target: $dstPath"
        }

        for ($i = 0; $i -lt $srcBytes.Length; $i++) {
            if ($srcBytes[$i] -ne $dstBytes[$i]) {
                throw "Binary comparison failed. Content mismatch: Source: $srcPath Target: $dstPath"
            }
        }
    }
}

function Resolve-TargetPath {
    <#
    .SYNOPSIS
        Resolves environment variable placeholders in target paths.
        Supports {UserProfile}, {AppData}, {LocalAppData}, {Home}.
    #>
    param([Parameter(Mandatory)] [string]$Template)
    $resolved = $Template
    $resolved = $resolved.Replace('{UserProfile}', $env:USERPROFILE)
    $resolved = $resolved.Replace('{AppData}', $env:APPDATA)
    $resolved = $resolved.Replace('{LocalAppData}', $env:LOCALAPPDATA)
    $resolved = $resolved.Replace('{Home}', $env:USERPROFILE)
    # Normalise to OS path separators
    $resolved = $resolved.Replace('/', '\')
    return $resolved
}

# ── YAML frontmatter parser ──────────────────────────────────────────────────

function Read-AgentFile {
    <#
    .SYNOPSIS
        Parses a .ai/agents/*.agent.md file into frontmatter fields and markdown body.
    .DESCRIPTION
        Extracts YAML frontmatter between --- delimiters, converts inline JSON arrays
        via ConvertFrom-Json, and returns a PSCustomObject with typed properties.
    #>
    param([Parameter(Mandatory)] [string]$Path)

    $content = Get-TextAuto -Path $Path
    # Strip .agent.md (double extension) or .md to get the slug
    $fileName = [System.IO.Path]::GetFileName($Path)
    $slug = $fileName -replace '\.agent\.md$', '' -replace '\.md$', ''

    $defaults = @{
        Slug        = $slug
        Name        = $slug
        Description = ''
        Tools       = @()
        Groups      = @('read', 'edit', 'command')
        Body        = $content
    }

    # Split content into lines for structured parsing
    $lines = $content -split "`n"

    # Verify the file starts with a YAML frontmatter delimiter
    if ($lines.Count -lt 3 -or $lines[0].Trim() -ne '---') {
        return [PSCustomObject]$defaults
    }

    # Find the closing --- delimiter (starting from line 1)
    $closingIndex = -1
    for ($i = 1; $i -lt $lines.Count; $i++) {
        if ($lines[$i].Trim() -eq '---') {
            $closingIndex = $i
            break
        }
    }
    if ($closingIndex -lt 0) {
        return [PSCustomObject]$defaults
    }

    # Extract frontmatter lines and body
    $fmLines = $lines[1..($closingIndex - 1)]
    $bodyLines = if ($closingIndex + 1 -lt $lines.Count) { $lines[($closingIndex + 1)..($lines.Count - 1)] } else { @() }
    $defaults.Body = ($bodyLines -join "`n").Trim()

    # Parse each frontmatter key-value line
    foreach ($fmLine in $fmLines) {
        $trimmed = $fmLine.Trim()
        if ($trimmed -eq '' -or $trimmed.StartsWith('#')) { continue }

        # Split on first colon only
        $colonPos = $trimmed.IndexOf(':')
        if ($colonPos -lt 1) { continue }

        $key = $trimmed.Substring(0, $colonPos).Trim()
        $val = $trimmed.Substring($colonPos + 1).Trim()

        # Strip surrounding quotes from scalar values
        if (($val.StartsWith('"') -and $val.EndsWith('"')) -or
            ($val.StartsWith("'") -and $val.EndsWith("'"))) {
            $val = $val.Substring(1, $val.Length - 2)
        }

        switch ($key) {
            'name'        { $defaults.Name = $val }
            'description' { $defaults.Description = $val }
            'tools' {
                # Inline JSON array — deserialise safely
                if ($val.StartsWith('[')) {
                    $defaults.Tools = @(($val | ConvertFrom-Json))
                }
            }
            'groups' {
                if ($val.StartsWith('[')) {
                    $defaults.Groups = @(($val | ConvertFrom-Json))
                }
            }
        }
    }

    return [PSCustomObject]$defaults
}

# ── Sync operations ──────────────────────────────────────────────────────────

function Sync-MultipleFileInstructions {
    param(
        [Parameter(Mandatory)] [string]$AgentName,
        [Parameter(Mandatory)] [string]$TargetDirectory,
        [Parameter(Mandatory)] [System.IO.FileSystemInfo[]]$SourceFiles
    )

    Write-Host "`r`n--- Updating $AgentName Instructions ---"
    $targetDir = Join-Path $repoRoot $TargetDirectory

    foreach ($sourceFile in $SourceFiles) {
        $targetFile = Join-Path $targetDir $sourceFile.Name
        Copy-FileIfDifferent -SourcePath $sourceFile.FullName -TargetPath $targetFile
    }

    Assert-InstructionSync -SourceDirectory ".ai" -TargetDirectory $TargetDirectory -SourceFiles $SourceFiles
}

function Sync-SingleFileInstructions {
    param(
        [Parameter(Mandatory)] [string]$AgentName,
        [Parameter(Mandatory)] [string]$TargetFilePath,
        [Parameter(Mandatory)] [System.IO.FileSystemInfo[]]$SourceFiles
    )

    Write-Host "`r`n--- Updating $AgentName Instructions ---"
    $targetFile = Join-Path $repoRoot $TargetFilePath
    $relativeTarget = $targetFile.Substring($repoRoot.Length + 1)

    $allContent = New-Object System.Text.StringBuilder
    $firstFile = $true

    foreach ($sourceFile in $SourceFiles) {
        $sourceContent = Get-TextAuto -Path $sourceFile.FullName
        if ([string]::IsNullOrWhiteSpace($sourceContent)) {
            Write-Warning "Skipping empty file: $($sourceFile.Name)"
            continue
        }
        if (-not $firstFile) {
            [void]$allContent.AppendLine("")
        }
        [void]$allContent.AppendLine("==== START OF INSTRUCTIONS FROM: $($sourceFile.Name) ====")
        [void]$allContent.AppendLine("")
        [void]$allContent.AppendLine("# Instructions from: $($sourceFile.Name)")
        [void]$allContent.AppendLine("")
        [void]$allContent.AppendLine($sourceContent.Trim())
        [void]$allContent.AppendLine("")
        [void]$allContent.AppendLine("==== END OF INSTRUCTIONS FROM: $($sourceFile.Name) ====")
        $firstFile = $false
    }

    $finalContent = $allContent.ToString()
    $existing = if (Test-Path -Path $targetFile -PathType Leaf) { Get-TextAuto -Path $targetFile } else { $null }
    if ($null -ne $existing -and $existing -eq $finalContent) {
        Write-Host "Up-to-date: $relativeTarget"
        return
    }

    Write-Utf8NoBom -Path $targetFile -Content $finalContent
    Write-Host "Updated: $relativeTarget"
}

function Sync-CopilotFolderInstructions {
    param(
        [Parameter(Mandatory)] [PSCustomObject]$InstructionsConfig,
        [Parameter(Mandatory)] [System.IO.FileSystemInfo[]]$SourceFiles
    )

    Write-Host "`r`n--- Updating GitHub CoPilot Instructions (folder-based) ---"

    $mainName = $InstructionsConfig.mainFile
    $mainSource = $SourceFiles | Where-Object { $_.Name -ieq $mainName } | Select-Object -First 1
    if ($null -eq $mainSource) {
        throw "Expected source '$mainName' under .ai but none found."
    }

    $copilotTarget = Join-Path $repoRoot $InstructionsConfig.target
    Copy-FileIfDifferent -SourcePath $mainSource.FullName -TargetPath $copilotTarget

    $folderTarget = Join-Path $repoRoot $InstructionsConfig.folderTarget
    foreach ($sf in $SourceFiles) {
        if ($sf.Name -ieq $mainName) { continue }
        $destination = Join-Path $folderTarget $sf.Name
        Copy-FileIfDifferent -SourcePath $sf.FullName -TargetPath $destination
    }
}

function Invoke-RoboCopyMirror {
    param(
        [Parameter(Mandatory)] [string]$SourceDirectory,
        [Parameter(Mandatory)] [string]$DestinationDirectory,
        [Parameter(Mandatory)] [string]$Label
    )

    if (-not (Test-Path $SourceDirectory -PathType Container)) {
        Write-Host "No source folder found at: $SourceDirectory"
        return
    }

    Ensure-Directory -Path $DestinationDirectory

    Write-Host "`r`n--- Mirroring to $Label ---"
    Write-Host "Source:      $SourceDirectory"
    Write-Host "Destination: $DestinationDirectory"

    $excludedDirs = @('.git', '.vs', 'bin', 'obj')

    $roboArgs = @(
        $SourceDirectory,
        $DestinationDirectory,
        '/MIR', '/FFT', '/R:1', '/W:1',
        '/NFL', '/NDL', '/NJH', '/NJS', '/NP'
    )

    foreach ($d in $excludedDirs) {
        $roboArgs += '/XD'
        $roboArgs += $d
    }

    Write-Host "robocopy <source> <destination> /MIR /NFL /NDL /NJH /NJS /NP ..."

    & robocopy @roboArgs | Out-Null
    $exitCode = $LASTEXITCODE

    # Robocopy exit codes 0-7 are success; >= 8 is failure.
    if ($exitCode -ge 8) {
        throw "Robocopy failed with exit code $exitCode."
    }

    $global:LASTEXITCODE = 0
    Write-Host "Mirrored to $Label (robocopy exit code $exitCode)."
}

function Build-RoomodesJson {
    <#
    .SYNOPSIS
        Builds a .roomodes-format JSON string from a directory of agent .md files.
    .DESCRIPTION
        Reads all *.agent.md files from SourceDirectory, parses frontmatter, and produces
        a {"customModes": [...]} JSON string using ConvertFrom-Json/ConvertTo-Json
        round-trip for safe serialisation on Windows PowerShell 5.1.
    #>
    param(
        [Parameter(Mandatory)] [string]$SourceDirectory,
        [Parameter(Mandatory)] [string]$Label
    )

    $agentFiles = @(Get-ChildItem -Path $SourceDirectory -Filter '*.agent.md' -File | Sort-Object Name)
    if ($agentFiles.Count -eq 0) {
        Write-Host "  No agent files found in: $SourceDirectory"
        return '{"customModes":[]}'
    }

    $modesJson = New-Object System.Text.StringBuilder
    [void]$modesJson.Append('[')
    $first = $true

    foreach ($af in $agentFiles) {
        $parsed = Read-AgentFile -Path $af.FullName
        Write-Host "  [$Label] $($parsed.Name) (slug: $($parsed.Slug))"

        $modeObj = [PSCustomObject]@{
            slug               = [string]$parsed.Slug
            name               = [string]$parsed.Name
            roleDefinition     = [string]$parsed.Description
            customInstructions = [string]$parsed.Body
            groups             = @([string[]]$parsed.Groups)
        }
        $modeJsonStr = $modeObj | ConvertTo-Json -Depth 5 -Compress

        if (-not $first) { [void]$modesJson.Append(',') }
        [void]$modesJson.Append($modeJsonStr)
        $first = $false
    }

    [void]$modesJson.Append(']')

    # Pretty-print via round-trip
    $wrapperJson = '{"customModes":' + $modesJson.ToString() + '}'
    $roundTripped = $wrapperJson | ConvertFrom-Json
    return ($roundTripped | ConvertTo-Json -Depth 10)
}

function Sync-AgentsToTarget {
    <#
    .SYNOPSIS
        Syncs agent .md files from a source directory to a target using the specified format.
    .DESCRIPTION
        Supports 'mirror' (robocopy) and 'roomodes-json' (transform to .roomodes JSON) formats.
    #>
    param(
        [Parameter(Mandatory)] [string]$AgentName,
        [Parameter(Mandatory)] [string]$SourceDirectory,
        [Parameter(Mandatory)] [string]$TargetPath,
        [string]$Format = 'mirror',
        [string]$Label = ''
    )

    if (-not (Test-Path $SourceDirectory -PathType Container)) {
        Write-Host "No agent source folder: $SourceDirectory"
        return
    }

    $agentFiles = @(Get-ChildItem -Path $SourceDirectory -Filter '*.agent.md' -File)
    if ($agentFiles.Count -eq 0) {
        return
    }

    if ($Label -eq '') { $Label = $AgentName }

    switch ($Format) {
        'roomodes-json' {
            Write-Host "`r`n--- Building $Label custom modes ($TargetPath) ---"

            $newJson = Build-RoomodesJson -SourceDirectory $SourceDirectory -Label $Label

            $existing = if (Test-Path $TargetPath -PathType Leaf) { Get-TextAuto -Path $TargetPath } else { $null }
            if ($null -ne $existing -and $existing.Trim() -eq $newJson.Trim()) {
                Write-Host "Up-to-date: $TargetPath"
            }
            else {
                Write-Utf8NoBom -Path $TargetPath -Content $newJson
                Write-Host "Updated: $TargetPath"
            }
        }
        default {
            # Direct mirror (Copilot, Claude)
            Invoke-RoboCopyMirror -SourceDirectory $SourceDirectory -DestinationDirectory $TargetPath -Label "$Label agents"
        }
    }
}

# ── Agent detection (AUTO mode) ──────────────────────────────────────────────

function Test-AgentExists {
    param([Parameter(Mandatory)] [PSCustomObject]$Agent)

    $instr = $Agent.instructions
    $target = Join-Path $repoRoot $instr.target

    switch ($instr.mode) {
        'multiple-files' {
            return (Test-HasInstructionFiles -Path $target)
        }
        'single-file' {
            return (Test-Path $target -PathType Leaf)
        }
        default {
            return $false
        }
    }
}

function Get-AgentFormat {
    <#
    .SYNOPSIS
        Safely reads the 'format' property from an agents/globalAgents config object.
        Returns 'mirror' if the property does not exist.
    #>
    param([Parameter(Mandatory)] [PSCustomObject]$ConfigObj)
    if ($ConfigObj.PSObject.Properties.Match('format').Count -gt 0 -and $null -ne $ConfigObj.format) {
        return $ConfigObj.format
    }
    return 'mirror'
}

# ── Main logic ───────────────────────────────────────────────────────────────

if (-not $NoClear) {
    Clear-Host
}

# Discover source instruction files
[System.IO.FileSystemInfo[]]$sourceInstructionFiles = Get-ChildItem -Path $aiDir -Filter "*instructions.md" -File | Sort-Object Name

if ($null -eq $sourceInstructionFiles -or $sourceInstructionFiles.Length -eq 0) {
    Write-Warning "No '*instructions.md' files found in '$aiDir'. Nothing to process."
    exit 0
}

Write-Host "Source instruction files in '$aiDir':"
$sourceInstructionFiles | ForEach-Object { Write-Host "- $($_.Name)" }

# Source paths
$srcSkillsDir = Join-Path $repoRoot ".ai\skills"
$srcAgentsDir = Join-Path $repoRoot ".ai\agents"
$srcGlobalAgentsDir = Join-Path $repoRoot ".ai\.global\agents"

# Show global mode status
if ($Global) {
    Write-Host "`r`nGlobal agent sync: ENABLED (-Global flag)"
}

# Build the list of agents to update based on mode
$allAgents = $config.agents
$agentsToUpdate = @()

if ($Mode -eq 'ALL') {
    Write-Host "Selected: ALL (parameter mode)"
    $agentsToUpdate = $allAgents
}
elseif ($Mode -eq 'AUTO') {
    Write-Host "Selected: AUTO (parameter mode)"
    foreach ($agent in $allAgents) {
        if (Test-AgentExists -Agent $agent) {
            $agentsToUpdate += $agent
        }
    }
    Write-Host "Agents to update:"
    foreach ($a in $agentsToUpdate) { Write-Host "- $($a.name)" }
}
elseif ($Mode -eq '') {
    # Interactive menu — detect available agents and let user choose
    $detectedAgents = @()
    foreach ($agent in $allAgents) {
        if (Test-AgentExists -Agent $agent) {
            $detectedAgents += $agent
        }
    }

    Write-Host "`r`nDetected agents with instruction files:"
    if ($detectedAgents.Count -gt 0) {
        foreach ($a in $detectedAgents) { Write-Host "- $($a.name)" }
    }
    else {
        Write-Host "(none)"
    }

    Write-Host ""
    Write-Host "=============================================================="
    Write-Host "Select Agent Instruction Set to Update"
    Write-Host "--------------------------------------------------------------"
    Write-Host "1. AUTO           - Update detected agents (project level only)"
    Write-Host "2. AUTO + Global  - Update detected agents + global agents"
    Write-Host "3. ALL            - Update all agents (project level only)"
    Write-Host "4. ALL + Global   - Update all agents + global agents"
    Write-Host "--------------------------------------------------------------"
    $i = 5
    foreach ($agent in $allAgents) {
        Write-Host "$i. $($agent.name)"
        $i++
    }
    Write-Host "0. Exit"
    Write-Host "=============================================================="
    Write-Host ""
    $selection = Read-Host "Enter your choice"

    switch ($selection) {
        '0' { Write-Host "Operation cancelled."; exit 0 }
        '1' { $agentsToUpdate = $detectedAgents; Write-Host "Selected: AUTO" }
        '2' { $agentsToUpdate = $detectedAgents; $Global = $true; Write-Host "Selected: AUTO + Global" }
        '3' { $agentsToUpdate = $allAgents; Write-Host "Selected: ALL" }
        '4' { $agentsToUpdate = $allAgents; $Global = $true; Write-Host "Selected: ALL + Global" }
        default {
            $idx = [int]$selection - 5
            if ($idx -ge 0 -and $idx -lt $allAgents.Count) {
                $agentsToUpdate = @($allAgents[$idx])
                Write-Host "Selected: $($allAgents[$idx].name)"
            }
            else {
                throw "Invalid selection."
            }
        }
    }
}
else {
    # Specific agent by name (case-insensitive)
    $found = $allAgents | Where-Object { $_.name -ieq $Mode -or $_.id -ieq $Mode }
    if ($null -eq $found) {
        $validNames = ($allAgents | ForEach-Object { $_.name }) -join ', '
        throw "Unknown agent '$Mode'. Valid agents: $validNames"
    }
    $agentsToUpdate = @($found)
    Write-Host "Selected: $($found.name) (parameter mode)"
}

# ── Sync each agent (project level) ─────────────────────────────────────────

foreach ($agent in $agentsToUpdate) {
    $instr = $agent.instructions

    # --- Instructions ---
    switch ($instr.mode) {
        'multiple-files' {
            Sync-MultipleFileInstructions -AgentName $agent.name -TargetDirectory $instr.target -SourceFiles $sourceInstructionFiles
        }
        'single-file' {
            $folderTarget = $null
            if ($instr.PSObject.Properties.Match('folderTarget').Count -gt 0) {
                $folderTarget = $instr.folderTarget
            }
            if ($null -ne $folderTarget -and (Test-Path (Join-Path $repoRoot $folderTarget) -PathType Container)) {
                Sync-CopilotFolderInstructions -InstructionsConfig $instr -SourceFiles $sourceInstructionFiles
            }
            else {
                Sync-SingleFileInstructions -AgentName $agent.name -TargetFilePath $instr.target -SourceFiles $sourceInstructionFiles
            }
        }
    }

    # --- Skills ---
    if ($null -ne $agent.skills -and $null -ne $agent.skills.target) {
        $dstSkillsDir = Join-Path $repoRoot $agent.skills.target
        Invoke-RoboCopyMirror -SourceDirectory $srcSkillsDir -DestinationDirectory $dstSkillsDir -Label "$($agent.name) skills ($($agent.skills.target))"
    }

    # --- Project-level custom agents ---
    if ($null -ne $agent.agents -and $null -ne $agent.agents.target) {
        $projTarget = Join-Path $repoRoot $agent.agents.target
        $projFormat = Get-AgentFormat -ConfigObj $agent.agents
        Sync-AgentsToTarget -AgentName $agent.name -SourceDirectory $srcAgentsDir -TargetPath $projTarget -Format $projFormat -Label "$($agent.name) project"
    }
}

# ── Global agent sync (only with -Global flag) ──────────────────────────────

if ($Global) {
    Write-Host "`r`n=============================================================="
    Write-Host "Global Agent Sync"
    Write-Host "=============================================================="

    if (-not (Test-Path $srcGlobalAgentsDir -PathType Container)) {
        Write-Host "No global agents source folder: $srcGlobalAgentsDir"
    }
    else {
        $globalFiles = @(Get-ChildItem -Path $srcGlobalAgentsDir -Filter '*.agent.md' -File)
        Write-Host "Global agent source files ($($globalFiles.Count)):"
        $globalFiles | ForEach-Object { Write-Host "- $($_.Name)" }

        foreach ($agent in $agentsToUpdate) {
            # Check if this agent has a globalAgents config
            if ($agent.PSObject.Properties.Match('globalAgents').Count -eq 0) { continue }
            $gaCfg = $agent.globalAgents
            if ($null -eq $gaCfg -or $null -eq $gaCfg.target) { continue }

            $gaTarget = Resolve-TargetPath -Template $gaCfg.target
            $gaFormat = Get-AgentFormat -ConfigObj $gaCfg

            Sync-AgentsToTarget -AgentName $agent.name -SourceDirectory $srcGlobalAgentsDir -TargetPath $gaTarget -Format $gaFormat -Label "$($agent.name) GLOBAL"
        }
    }
}

Write-Host "`r`nAll selected operations completed successfully."

# Only pause when launched by double-click (Explorer).
if ($Host.Name -and $Host.Name -notlike '*ConsoleHost*') {
    Write-Host "Pausing for 2 seconds..."
    Start-Sleep -Seconds 2
}
