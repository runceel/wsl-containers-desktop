[CmdletBinding()]
param(
    [string]$ProjectPath = ".\src\WslContainersDesktop.App\WslContainersDesktop.App.csproj",

    # Store baseline is x64 + arm64. x86 is opt-in via -IncludeX86 or -Architectures.
    [ValidateSet('x64', 'arm64', 'x86', IgnoreCase = $true)]
    [string[]]$Architectures = @('x64', 'arm64'),

    [switch]$IncludeX86,

    [string]$Configuration = "Release",
    [string]$OutputDirectory,

    # -Sign additionally produces a separately-named, locally signed sideload
    # bundle ('<name>_<version>_sideload.msix[bundle]') plus its public
    # certificate ('devcert.cer'). These never affect the Store .msixupload,
    # which Microsoft Store re-signs during ingestion and must stay unsigned.
    [switch]$Sign,
    [string]$CertificatePath,
    [string]$CertificatePassword,
    [string]$CertificateThumbprint,
    [switch]$GenerateTemporaryStoreCertificate,

    [switch]$NoRestore,
    [switch]$SkipClean
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

function Assert-Command {
    param([Parameter(Mandatory)][string]$Name, [Parameter(Mandatory)][string]$InstallHint)
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found on PATH. $InstallHint"
    }
}

Assert-Command -Name "dotnet" -InstallHint "Install the .NET SDK."
Assert-Command -Name "winapp" -InstallHint "Install via 'winget install Microsoft.WinAppCLI'."

function New-RandomPassword {
    param([int]$Length = 24)

    $characters = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%^&*'
    $charArray = $characters.ToCharArray()
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    $bytes = New-Object byte[] $Length
    $rng.GetBytes($bytes)

    $builder = New-Object System.Text.StringBuilder
    for ($i = 0; $i -lt $Length; $i++) {
        [void]$builder.Append($charArray[$bytes[$i] % $charArray.Length])
    }

    return $builder.ToString()
}

function Assert-PackageImagesResolve {
    <#
      .SYNOPSIS
        Fails the build if any image referenced by the produced package's
        AppxManifest.xml cannot be resolved to a physically-present file --
        exactly the check Microsoft Store performs during ingestion, whose
        failure surfaces as "Package acceptance validation error: The following
        image(s) specified in the appxManifest.xml ... were not found".

      .DESCRIPTION
        The manifest references logo / tile / splash images by *logical* path
        (e.g. Assets\StoreLogo.png). The Store resolves each via MRT
        (resources.pri) to its scale/targetsize-qualified physical files and
        confirms those files exist inside the package. This function reproduces
        that resolution locally so a broken package is caught before upload
        instead of after a slow Partner Center round-trip:
          1. For a .msixbundle, inspect every inner architecture .msix; for a
             plain .msix, inspect that single package.
          2. Extract the package (it is a zip) and collect every manifest value
             that ends in an image extension.
          3. For each image, resolve through resources.pri: every candidate file
             the PRI maps the logical name to must exist. If the image is not
             indexed in the PRI, fall back to a literal file-existence check.
          4. Throw with the offending package + image list if anything is
             unresolved.
    #>
    param(
        [Parameter(Mandatory)][string]$PackagePath
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $imageExtensions = @('.png', '.jpg', '.jpeg', '.gif', '.ico')

    $workRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("msix-imgcheck-" + [System.Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force -Path $workRoot | Out-Null
    try {
        # Resolve the set of inner .msix packages to inspect.
        $innerPackages = [System.Collections.Generic.List[string]]::new()
        $extension = [System.IO.Path]::GetExtension($PackagePath)
        if ($extension -ieq '.msixbundle' -or $extension -ieq '.appxbundle') {
            $bundleDir = Join-Path $workRoot 'bundle'
            [System.IO.Compression.ZipFile]::ExtractToDirectory($PackagePath, $bundleDir)
            Get-ChildItem -LiteralPath $bundleDir -Filter '*.msix' -File |
                ForEach-Object { $innerPackages.Add($_.FullName) }
            if ($innerPackages.Count -eq 0) {
                throw "No inner .msix packages were found inside bundle '$PackagePath'."
            }
        } else {
            $innerPackages.Add($PackagePath)
        }

        $overallErrors = [System.Collections.Generic.List[string]]::new()

        foreach ($pkg in $innerPackages) {
            $pkgName = Split-Path -Leaf $pkg
            $pkgDir = Join-Path $workRoot ([System.IO.Path]::GetFileNameWithoutExtension($pkg))
            [System.IO.Compression.ZipFile]::ExtractToDirectory($pkg, $pkgDir)

            $manifestPath = Join-Path $pkgDir 'AppxManifest.xml'
            if (-not (Test-Path $manifestPath)) {
                $overallErrors.Add("[$pkgName] AppxManifest.xml is missing from the package.")
                continue
            }

            # Collect every manifest value (element text or attribute) that
            # references an image file. Scanning generically avoids hard-coding
            # the manifest schema, so new tile/badge images are covered too.
            [xml]$pkgManifest = Get-Content -LiteralPath $manifestPath -Raw
            $imageRefs = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
            foreach ($node in $pkgManifest.SelectNodes('//*')) {
                if ($node.ChildNodes.Count -eq 1 -and $node.FirstChild.NodeType -eq [System.Xml.XmlNodeType]::Text) {
                    $val = $node.InnerText.Trim()
                    if ($val -and ($imageExtensions -contains ([System.IO.Path]::GetExtension($val)).ToLowerInvariant())) {
                        [void]$imageRefs.Add($val)
                    }
                }
                foreach ($attr in $node.Attributes) {
                    $val = $attr.Value.Trim()
                    if ($val -and ($imageExtensions -contains ([System.IO.Path]::GetExtension($val)).ToLowerInvariant())) {
                        [void]$imageRefs.Add($val)
                    }
                }
            }

            if ($imageRefs.Count -eq 0) {
                continue
            }

            # Dump resources.pri (if present) to resolve MRT logical -> physical.
            $namedResources = $null
            $priPath = Join-Path $pkgDir 'resources.pri'
            if (Test-Path $priPath) {
                $priDumpPath = Join-Path $pkgDir '_imgcheck_pri_dump.xml'
                & winapp tool makepri dump /if $priPath /o /of $priDumpPath *> $null
                if (($LASTEXITCODE -eq 0) -and (Test-Path $priDumpPath)) {
                    [xml]$priXml = Get-Content -LiteralPath $priDumpPath -Raw
                    $namedResources = $priXml.SelectNodes('//NamedResource')
                }
            }

            $missingForPackage = [System.Collections.Generic.List[string]]::new()
            foreach ($ref in $imageRefs) {
                $logicalForward = ($ref -replace '\\', '/')
                $resolved = $false

                if ($namedResources) {
                    $match = $null
                    foreach ($nr in $namedResources) {
                        if ($nr.uri -and ($nr.uri -imatch ('/Files/' + [regex]::Escape($logicalForward) + '$'))) {
                            $match = $nr
                            break
                        }
                    }
                    if ($match) {
                        $candidateValues = @($match.Candidate | ForEach-Object { $_.Value } | Where-Object { $_ })
                        if ($candidateValues.Count -gt 0) {
                            $missingCandidates = @()
                            foreach ($cv in $candidateValues) {
                                $candidatePath = Join-Path $pkgDir ($cv -replace '\\', '/')
                                if (-not (Test-Path -LiteralPath $candidatePath)) {
                                    $missingCandidates += $cv
                                }
                            }
                            $resolved = $true  # image is accounted for via the PRI
                            if ($missingCandidates.Count -gt 0) {
                                $missingForPackage.Add("$ref (resources.pri maps it to missing file(s): $($missingCandidates -join ', '))")
                            }
                        }
                    }
                }

                if (-not $resolved) {
                    # Not indexed in the PRI: require the literal file to exist.
                    $literalPath = Join-Path $pkgDir $logicalForward
                    if (-not (Test-Path -LiteralPath $literalPath)) {
                        $missingForPackage.Add("$ref (no resources.pri entry and no literal file in package)")
                    }
                }
            }

            if ($missingForPackage.Count -gt 0) {
                $overallErrors.Add("[$pkgName] unresolved manifest image(s):`n      - " + ($missingForPackage -join "`n      - "))
            } else {
                Write-Host "    [$pkgName] all $($imageRefs.Count) manifest image(s) resolve to present files."
            }
        }

        if ($overallErrors.Count -gt 0) {
            throw ("Store image validation FAILED for '$([System.IO.Path]::GetFileName($PackagePath))'. " +
                "Microsoft Store would reject this package with 'image(s) ... were not found'.`n" +
                ($overallErrors -join "`n") +
                "`n`nEnsure the manifest's image assets and their scale-qualified variants are packaged " +
                "(Content items in WslContainersDesktop.App.csproj must include Assets\*.png with " +
                "CopyToPublishDirectory=PreserveNewest).")
        }
    }
    finally {
        Remove-Item -LiteralPath $workRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# ---------------------------------------------------------------------------
# Resolve paths
# ---------------------------------------------------------------------------
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptRoot "..")
$projectFile = (Resolve-Path (Join-Path $repoRoot $ProjectPath) -ErrorAction Stop).Path
$projectDir = Split-Path -Parent $projectFile
$manifestFile = Join-Path $projectDir "Package.appxmanifest"
if (-not (Test-Path $manifestFile)) {
    throw "Manifest not found: $manifestFile"
}

# ---------------------------------------------------------------------------
# Architecture normalisation (dedup, preserve first-seen order)
# ---------------------------------------------------------------------------
$archTable = @{
    'x64'   = [pscustomobject]@{ Key = 'x64';   Platform = 'x64';   Rid = 'win-x64'   }
    'arm64' = [pscustomobject]@{ Key = 'arm64'; Platform = 'ARM64'; Rid = 'win-arm64' }
    'x86'   = [pscustomobject]@{ Key = 'x86';   Platform = 'x86';   Rid = 'win-x86'   }
}

$resolvedArchs = [System.Collections.Generic.List[object]]::new()
$seenArchs = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($a in $Architectures) {
    $key = $a.ToLowerInvariant()
    if ($seenArchs.Add($key)) {
        [void]$resolvedArchs.Add($archTable[$key])
    }
}
if ($IncludeX86 -and $seenArchs.Add('x86')) {
    [void]$resolvedArchs.Add($archTable['x86'])
}
if ($resolvedArchs.Count -eq 0) {
    throw "No target architectures resolved."
}

# ---------------------------------------------------------------------------
# Manifest identity (Name / Version / Publisher) for output naming and
# per-architecture sanity checks
# ---------------------------------------------------------------------------
[xml]$manifestXml = Get-Content -LiteralPath $manifestFile -Raw
$identity = $manifestXml.Package.Identity
if (-not $identity) {
    throw "Manifest at $manifestFile is missing <Identity>."
}

$packageName = $identity.Name
$packageVersion = $identity.Version
$packagePublisher = $identity.Publisher
if (-not $packageName -or -not $packageVersion -or -not $packagePublisher) {
    throw "Manifest Identity is missing Name/Version/Publisher."
}

if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $projectDir "AppPackages"
}
$OutputDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)

Write-Host "==> WSL Containers Desktop Store package build" -ForegroundColor Cyan
Write-Host "    Identity Name      : $packageName"
Write-Host "    Identity Publisher : $packagePublisher"
Write-Host "    Version            : $packageVersion"
Write-Host "    Architectures      : $((($resolvedArchs | ForEach-Object Key) -join ', '))"
Write-Host "    Output directory   : $OutputDirectory"

# ---------------------------------------------------------------------------
# Clean output (preserve dev signing certificate across incremental -Sign runs)
# ---------------------------------------------------------------------------
$layoutRoot = Join-Path $OutputDirectory "layout"

if (-not $SkipClean -and (Test-Path $OutputDirectory)) {
    Write-Host "==> Cleaning $OutputDirectory (preserving devcert.pfx / devcert.cer)" -ForegroundColor Cyan
    Get-ChildItem -LiteralPath $OutputDirectory -Force |
        Where-Object { $_.Name -ne 'devcert.pfx' -and $_.Name -ne 'devcert.cer' } |
        Remove-Item -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $OutputDirectory, $layoutRoot | Out-Null

# ---------------------------------------------------------------------------
# Per-architecture: self-contained dotnet publish
# ---------------------------------------------------------------------------
$publishDirs = [System.Collections.Generic.List[string]]::new()
foreach ($arch in $resolvedArchs) {
    Write-Host ""
    Write-Host "==> [$($arch.Key)] dotnet publish ($Configuration, $($arch.Rid))" -ForegroundColor Cyan

    $publishDir = Join-Path $layoutRoot $arch.Key
    New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

    # `dotnet publish` produces the self-contained layout (with AppxManifest.xml
    # token-replaced) under -o. Trimming is forcibly disabled: WinUI 3 /
    # CommunityToolkit.Mvvm reflection-heavy code paths are not fully
    # trim-safe. GenerateAppxPackageOnBuild=false ensures MSBuild does not try
    # to invoke MakeAppx itself -- `winapp package` below owns that step.
    $dotnetArgs = @(
        'publish', $projectFile
        '-c', $Configuration
        '-p:Platform=' + $arch.Platform
        '-p:RuntimeIdentifier=' + $arch.Rid
        '-p:SelfContained=true'
        '-p:PublishTrimmed=false'
        '-p:GenerateAppxPackageOnBuild=false'
        '-p:AppxPackageSigningEnabled=false'
        '-o', $publishDir
        '--nologo'
        '-v', 'minimal'
    )
    if ($NoRestore) {
        $dotnetArgs += '--no-restore'
    }
    & dotnet @dotnetArgs
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for $($arch.Key) (exit $LASTEXITCODE)"
    }

    # WslContainersDesktop.App.csproj marks Assets\*.png / Assets\AppIcon.ico
    # with CopyToPublishDirectory="PreserveNewest", so `dotnet publish` copies
    # them into the publish output even with GenerateAppxPackageOnBuild=false
    # (that flag only disables the MSIX single-project packaging targets, not
    # the normal Content-copy mechanism, once the metadata is set). This is a
    # fail-fast safety net: if the csproj setting ever regresses, the manifest's
    # image references (StoreLogo.png, Square44x44Logo.png, etc.) would resolve
    # to nothing and Partner Center would reject the package with
    # "Package acceptance validation error: ... were not found".
    $assetsDestDir = Join-Path $publishDir 'Assets'
    if (-not (Get-ChildItem -LiteralPath $assetsDestDir -Filter '*.png' -ErrorAction SilentlyContinue)) {
        throw "Published output for $($arch.Key) has no Assets\*.png files. Check that Content items in WslContainersDesktop.App.csproj have CopyToPublishDirectory set; manifest image references will fail Store validation otherwise."
    }

    # Verify the published layout has the token-replaced manifest. If the
    # publish target dropped it, fall back to the canonical
    # bin\<Platform>\<Configuration>\<TFM>\<RID>\AppxManifest.xml.
    $publishedManifest = Join-Path $publishDir 'AppxManifest.xml'
    if (-not (Test-Path $publishedManifest)) {
        $tfmRoots = Get-ChildItem -LiteralPath (Join-Path $projectDir "bin\$($arch.Platform)\$Configuration") -Directory -ErrorAction SilentlyContinue
        $fallback = $null
        foreach ($tfm in $tfmRoots) {
            $candidate = Join-Path $tfm.FullName "$($arch.Rid)\AppxManifest.xml"
            if (Test-Path $candidate) { $fallback = $candidate; break }
        }
        if (-not $fallback) {
            throw "Published layout for $($arch.Key) is missing AppxManifest.xml and no build-output fallback was found under bin\$($arch.Platform)\$Configuration."
        }
        Write-Host "    AppxManifest.xml missing from publish dir; copying from $fallback" -ForegroundColor Yellow
        Copy-Item -LiteralPath $fallback -Destination $publishedManifest
    }

    # Sanity check: token replacement and arch tagging
    $manifestText = Get-Content -LiteralPath $publishedManifest -Raw
    if ($manifestText -match '\$targetnametoken\$|\$targetentrypoint\$') {
        throw "Published AppxManifest.xml still contains MSBuild tokens for $($arch.Key)."
    }
    if ($manifestText -notmatch 'ProcessorArchitecture\s*=\s*"' + [regex]::Escape($arch.Rid.Substring(4)) + '"') {
        throw "Published AppxManifest.xml for $($arch.Key) does not declare ProcessorArchitecture=`"$($arch.Rid.Substring(4))`"."
    }

    Write-Host "    Published layout: $publishDir"
    $publishDirs.Add($publishDir)
}

# ---------------------------------------------------------------------------
# Package / bundle: `winapp package` accepts multiple input folders and
# produces a multi-architecture .msixbundle directly (a single folder yields
# a plain .msix). No --cert here: the Store artifact must stay unsigned
# because Microsoft Store re-signs every package during ingestion.
# ---------------------------------------------------------------------------
Write-Host ""
$packageExtension = if ($publishDirs.Count -gt 1) { ".msixbundle" } else { ".msix" }
$packageFileName = "${packageName}_${packageVersion}${packageExtension}"
$packageOut = Join-Path $OutputDirectory $packageFileName
Write-Host "==> Packaging $($publishDirs.Count) architecture(s) into $packageFileName" -ForegroundColor Cyan

$winappPackageArgs = @($publishDirs) + @('--output', $packageOut, '--quiet')
& winapp package @winappPackageArgs
if ($LASTEXITCODE -ne 0) {
    throw "winapp package failed (exit $LASTEXITCODE)"
}
if (-not (Test-Path $packageOut)) {
    throw "Expected package not found: $packageOut"
}

$packageSizeMB = [math]::Round((Get-Item $packageOut).Length / 1MB, 2)
Write-Host "    Produced $packageFileName ($packageSizeMB MB)"

# ---------------------------------------------------------------------------
# Validate that every manifest-referenced image resolves inside the package
# BEFORE wrapping/uploading. This reproduces the Microsoft Store ingestion
# check whose failure reads "image(s) ... were not found", converting a slow
# Partner Center rejection into an immediate, local, actionable error.
# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "==> Validating manifest image assets resolve inside the package" -ForegroundColor Cyan
Assert-PackageImagesResolve -PackagePath $packageOut
Write-Host "    Image validation passed."

# ---------------------------------------------------------------------------
# Wrap into .msixupload (a zip containing the bundle/package at the root)
#
# IMPORTANT: This must happen BEFORE optional signing, because the Store
# .msixupload must contain the UNSIGNED package, not the locally-signed
# sideload copy produced below.
# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "==> Wrapping unsigned package into .msixupload (Store submission artifact)" -ForegroundColor Cyan
$uploadName = "${packageName}_${packageVersion}.msixupload"
$uploadOut = Join-Path $OutputDirectory $uploadName
if (Test-Path $uploadOut) { Remove-Item -LiteralPath $uploadOut -Force }

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::Open($uploadOut, [System.IO.Compression.ZipArchiveMode]::Create)
try {
    [void][System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile(
        $zip,
        $packageOut,
        $packageFileName,
        [System.IO.Compression.CompressionLevel]::Optimal)
} finally {
    $zip.Dispose()
}

$uploadSizeMB = [math]::Round((Get-Item $uploadOut).Length / 1MB, 2)
Write-Host "    Produced $uploadName ($uploadSizeMB MB)"

try {
    $zip = [System.IO.Compression.ZipFile]::OpenRead($uploadOut)
    try {
        $entries = $zip.Entries.FullName
        if ($entries -notcontains $packageFileName) {
            throw "The .msixupload does not contain $packageFileName at its root. Entries: $($entries -join ', ')"
        }
    } finally {
        $zip.Dispose()
    }
} catch {
    throw "Verification of $uploadOut failed: $_"
}

# ---------------------------------------------------------------------------
# Optional: produce a signed sideload copy (GitHub Releases / local
# Add-AppxPackage testing only). This never touches the Store .msixupload.
# ---------------------------------------------------------------------------
$sideloadOut = $null
$cerOut = $null
if ($Sign) {
    Write-Host ""
    Write-Host "==> Producing signed sideload package" -ForegroundColor Cyan

    $sideloadName = "${packageName}_${packageVersion}_sideload${packageExtension}"
    $sideloadOut = Join-Path $OutputDirectory $sideloadName
    Copy-Item -LiteralPath $packageOut -Destination $sideloadOut -Force

    if ($CertificateThumbprint) {
        Write-Host "    Signing with certificate thumbprint $CertificateThumbprint from the local certificate store"
        & winapp tool signtool sign /sha1 $CertificateThumbprint /fd SHA256 /a $sideloadOut
        if ($LASTEXITCODE -ne 0) { throw "signtool sign failed (exit $LASTEXITCODE)" }
    } else {
        if (-not $CertificatePath) {
            $CertificatePath = Join-Path $OutputDirectory "devcert.pfx"
            $cerOut = Join-Path $OutputDirectory "devcert.cer"
            if (-not $CertificatePassword) {
                $CertificatePassword = New-RandomPassword
            }
            if ($GenerateTemporaryStoreCertificate -or -not (Test-Path $CertificatePath)) {
                Write-Host "    Generating dev certificate at $CertificatePath (matches manifest Publisher '$packagePublisher')"
                & winapp cert generate --manifest $manifestFile --output $CertificatePath --password $CertificatePassword --export-cer --if-exists Overwrite --quiet
                if ($LASTEXITCODE -ne 0) { throw "winapp cert generate failed (exit $LASTEXITCODE)" }
            }
        } else {
            $CertificatePath = (Resolve-Path -Path $CertificatePath -ErrorAction Stop).Path
        }
        if (-not $CertificatePassword) {
            $CertificatePassword = 'password'
        }

        & winapp sign $sideloadOut $CertificatePath --password $CertificatePassword
        if ($LASTEXITCODE -ne 0) { throw "winapp sign failed (exit $LASTEXITCODE)" }
    }

    Write-Host "    Signed sideload package: $sideloadOut"
    if ($cerOut -and (Test-Path $cerOut)) {
        Write-Host "    Public certificate      : $cerOut"
        Write-Host "    NEVER publish the .pfx -- only the .cer (public key) is safe to distribute." -ForegroundColor Yellow
    }
}

# ---------------------------------------------------------------------------
# Summary
# ---------------------------------------------------------------------------
Write-Host ""
Write-Host "==> Done." -ForegroundColor Green
Write-Host "    Package (unsigned, intermediate) : $packageOut"
Write-Host "    Upload (Store submission)        : $uploadOut"
if ($Sign) {
    Write-Host "    Sideload package (signed)        : $sideloadOut"
    if ($cerOut -and (Test-Path $cerOut)) {
        Write-Host "    Public cert (.cer)               : $cerOut"
    }
}

$uploadOut
