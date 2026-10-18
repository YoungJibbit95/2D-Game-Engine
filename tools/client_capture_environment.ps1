function Initialize-ClientCaptureEnvironment {
    param([Parameter(Mandatory)] [string] $RepositoryRoot)
    # Identity belongs to the actual rendering implementation, not the source commit.
    if (-not $env:YJSE_RENDER_ENVIRONMENT) {
        if ($IsWindows) {
            if ($env:SDL_OPENGL_LIBRARY) {
                $mesaBin = Split-Path -Parent $env:SDL_OPENGL_LIBRARY
                $env:YJSE_RENDER_ENVIRONMENT = (Get-FileHash -Algorithm SHA256 -LiteralPath $env:SDL_OPENGL_LIBRARY, (Join-Path $mesaBin 'libgallium_wgl.dll')).Hash -join ':'
            } else {
                $env:YJSE_RENDER_ENVIRONMENT = (Get-CimInstance Win32_VideoController | ForEach-Object { "$($_.Name):$($_.DriverVersion)" } | Sort-Object) -join ';'
            }
        } else {
            $env:YJSE_RENDER_ENVIRONMENT = (& dpkg-query -W '-f=${Version}' libgl1-mesa-dri) -join ''
            if ($LASTEXITCODE -ne 0) { throw 'Cannot identify Mesa driver package.' }
        }
    }
    $env:YJSE_CAPTURE_CPU = if ($IsWindows) {
        (Get-CimInstance Win32_Processor | Select-Object -First 1).Name
    } else {
        ((Get-Content -LiteralPath /proc/cpuinfo | Select-String '^model name\s*:' | Select-Object -First 1).Line -split ':', 2)[1].Trim()
    }
    $revision = & git -C $repositoryRoot rev-parse HEAD
    if ($LASTEXITCODE -ne 0) { throw 'Cannot identify source revision.' }
    $dirty = & git -C $repositoryRoot status --porcelain
    $env:YJSE_CAPTURE_REVISION = "$revision$(if ($dirty) { '+dirty' })"
}
