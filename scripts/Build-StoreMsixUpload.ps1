[CmdletBinding()]
param(
    [string]$ProjectPath = ".\src\WslContainersDesktop.App\WslContainersDesktop.App.csproj",
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$Platform = "x64",
    [string]$OutputDirectory,
    [string]$CertificatePath,
    [string]$CertificatePassword,
    [string]$CertificateThumbprint,
    [switch]$GenerateTemporaryStoreCertificate,
    [switch]$NoRestore
)

$ErrorActionPreference = "Stop"

function New-TemporarySigningCertificate {
    param(
        [Parameter(Mandatory)]
        [string]$Subject,
        [Parameter(Mandatory)]
        [string]$OutputPath,
        [Parameter(Mandatory)]
        [string]$Password
    )

    $tempDir = Split-Path -Parent $OutputPath
    if (-not (Test-Path $tempDir)) {
        New-Item -ItemType Directory -Path $tempDir -Force | Out-Null
    }

    $certificate = New-SelfSignedCertificate -Type CodeSigning -Subject $Subject -CertStoreLocation "Cert:\CurrentUser\My" -KeyAlgorithm RSA -KeyLength 2048 -HashAlgorithm SHA256 -KeyExportPolicy Exportable -NotAfter (Get-Date).AddYears(1)
    $securePassword = ConvertTo-SecureString -String $Password -Force -AsPlainText
    Export-PfxCertificate -Cert $certificate -FilePath $OutputPath -Password $securePassword | Out-Null

    return [pscustomobject]@{
        Path = $OutputPath
        Password = $Password
        Thumbprint = $certificate.Thumbprint
    }
}

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

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptRoot "..")
$projectFile = (Resolve-Path (Join-Path $repoRoot $ProjectPath) -ErrorAction Stop).Path
$projectDir = Split-Path -Parent $projectFile
$manifestFile = Join-Path $projectDir "Package.appxmanifest"
$manifestContent = Get-Content -Path $manifestFile -Raw
$manifestPublisherMatch = [regex]::Match($manifestContent, '<Identity\b[^>]*Publisher="([^"]+)"')
if (-not $manifestPublisherMatch.Success) {
    throw "Could not locate the package publisher in $manifestFile"
}
$publisher = $manifestPublisherMatch.Groups[1].Value

$resolvedCertificatePath = $null
$resolvedCertificatePassword = $null
$resolvedCertificateThumbprint = $null

if ($CertificatePath) {
    $resolvedCertificatePath = (Resolve-Path -Path $CertificatePath -ErrorAction Stop).Path
}

if ($CertificatePassword) {
    $resolvedCertificatePassword = $CertificatePassword
}

if ($CertificateThumbprint) {
    $resolvedCertificateThumbprint = $CertificateThumbprint
}

$generateTemporaryCertificate = $GenerateTemporaryStoreCertificate -or (-not $resolvedCertificatePath -and -not $resolvedCertificateThumbprint)
if ($generateTemporaryCertificate) {
    $tempCertDir = Join-Path ([System.IO.Path]::GetTempPath()) "wsl-containers-desktop-store-cert"
    $tempCertPath = Join-Path $tempCertDir "store-signing.pfx"
    $tempCertPassword = New-RandomPassword

    Write-Host "Generating temporary signing certificate for publisher '$publisher'"
    $tempCertificate = New-TemporarySigningCertificate -Subject $publisher -OutputPath $tempCertPath -Password $tempCertPassword
    $resolvedCertificatePath = $tempCertificate.Path
    $resolvedCertificatePassword = $tempCertificate.Password
    $resolvedCertificateThumbprint = $tempCertificate.Thumbprint
}

if ($OutputDirectory) {
    $resolvedOutputDirectory = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDirectory)
    if (-not (Test-Path $resolvedOutputDirectory)) {
        New-Item -ItemType Directory -Path $resolvedOutputDirectory -Force | Out-Null
    }
} else {
    $resolvedOutputDirectory = Join-Path $projectDir "AppPackages"
}

$buildArgs = @(
    $projectFile
)

if (-not $NoRestore) {
    $buildArgs += "/restore"
}

$buildArgs += @(
    "/t:Publish",
    "/p:Configuration=$Configuration",
    "/p:RuntimeIdentifier=$RuntimeIdentifier",
    "/p:Platform=$Platform",
    "/p:GenerateAppxPackageOnBuild=true",
    "/p:AppxPackageIsForStore=true",
    "/p:BuildAppxUploadPackageForUap=true",
    "/p:AppxPackageSigningEnabled=true",
    "/p:UapAppxPackageBuildMode=StoreUpload",
    "/p:AppxBundle=Always",
    "/p:AppxBundlePlatforms=$Platform",
    "/p:PublishTrimmed=false",
    "/p:AppxPackageDir=$resolvedOutputDirectory"
)

if ($resolvedCertificatePath) {
    $buildArgs += "/p:PackageCertificateKeyFile=$resolvedCertificatePath"
}

if ($resolvedCertificatePassword) {
    $buildArgs += "/p:PackageCertificatePassword=$resolvedCertificatePassword"
}

if ($resolvedCertificateThumbprint) {
    $buildArgs += "/p:PackageCertificateThumbprint=$resolvedCertificateThumbprint"
}

if ($NoRestore) {
    $buildArgs += "/p:Restore=false"
}

$packageSearchRoot = if ($OutputDirectory) { $resolvedOutputDirectory } else { $projectDir }
Get-ChildItem -Path $packageSearchRoot -Filter "*.msixupload" -Recurse -File -ErrorAction SilentlyContinue | Remove-Item -Force

Write-Host "Running: dotnet msbuild $($buildArgs -join ' ')"
& dotnet msbuild @buildArgs
$packageFiles = Get-ChildItem -Path $packageSearchRoot -Filter "*.msixupload" -Recurse -File | Sort-Object LastWriteTime -Descending
if ($LASTEXITCODE -ne 0 -and -not $packageFiles) {
    throw "MSIX Store upload build failed with exit code $LASTEXITCODE and no .msixupload package was produced."
}

if ($LASTEXITCODE -ne 0) {
    Write-Warning "The build exited with code $LASTEXITCODE, but an .msixupload package was produced. Review the packaging warnings before submission."
}

if (-not $packageFiles) {
    throw "No .msixupload package was produced under $packageSearchRoot."
}

$packageFile = $packageFiles[0]
Write-Host "Created: $($packageFile.FullName)"
$packageFile.FullName
