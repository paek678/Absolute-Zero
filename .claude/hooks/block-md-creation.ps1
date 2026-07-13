# block-md-creation.ps1
# PreToolUse hook: block .md file creation outside allowed locations
# Enforces Knowledge Proposal Protocol from CLAUDE.md
# Exit 0 = allow, Exit 2 = block

$input_json = [Console]::In.ReadToEnd()
$data = $input_json | ConvertFrom-Json

$filePath = $data.tool_input.file_path
if (-not $filePath) { exit 0 }

# Only check .md files
if ($filePath -notmatch '\.md$') { exit 0 }

# Normalize path separators
$normalized = $filePath -replace '\\', '/'

# Allowed .md file locations
$allowedPatterns = @(
    'CLAUDE\.md$',
    '/Docs/',
    'Docs/',
    '\.claude/skills/.+/SKILL\.md$',
    '/memory/',
    'MEMORY\.md$',
    '\.gitignore$'
)

$isAllowed = $false
foreach ($pattern in $allowedPatterns) {
    if ($normalized -match $pattern) {
        $isAllowed = $true
        break
    }
}

if (-not $isAllowed) {
    [Console]::Error.WriteLine("=== .md Creation Blocked (Knowledge Proposal Protocol) ===")
    [Console]::Error.WriteLine("File: $filePath")
    [Console]::Error.WriteLine("Allowed: CLAUDE.md, Docs/*.md, .claude/skills/*/SKILL.md, memory/*.md")
    [Console]::Error.WriteLine("Use 'Docs/' for documentation or propose changes via Knowledge Proposal Protocol")
    exit 2
}

exit 0
