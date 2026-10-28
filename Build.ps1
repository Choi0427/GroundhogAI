$ErrorActionPreference = 'Stop'
# Close this project's editor before a batch build; never close unrelated projects.
& unity build $PSScriptRoot --editor-version 6000.5.5f1 --target StandaloneWindows64 --output-path (Join-Path $PSScriptRoot 'Build\Windows\GroundhogAI.exe') --execute-method GroundhogAI.Editor.ProjectBuilder.BuildWindows --format json
if ($LASTEXITCODE -ne 0) { throw "Unity build failed ($LASTEXITCODE). See Unity build log." }
