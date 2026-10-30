param(
    [Parameter(Mandatory)]
    [ValidateSet('Run', 'Update', 'Stop')]
    [string]$Action,

    [string]$SettingsPath = (Join-Path $PSScriptRoot 'server-settings.json'),

    [ValidateSet('Refresh', 'Cached')]
    [string]$BaseImages = 'Refresh'
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Server-Aws.psm1') -Force
$settings = Get-Content -LiteralPath $SettingsPath -Raw | ConvertFrom-Json

foreach ($field in @('AccountId', 'Region', 'Profile', 'StackName', 'VpcId', 'SubnetId'))
{
    if (!$settings.$field)
    {
        throw "Missing server setting: $field"
    }
}

Get-Command aws -ErrorAction Stop | Out-Null

# Sign in only for credential errors; network or authorization failures must remain visible.
try
{
    $identity = Invoke-ServerAws -Settings $settings -Arguments @('sts', 'get-caller-identity', '--output', 'json') | ConvertFrom-Json
}
catch
{
    $credentialErrors = 'ExpiredToken|InvalidClientTokenId|Unable to locate credentials|NoCredentialsError|TokenRetrievalError|SSOTokenLoadError|Token has expired|profile .+ could not be found|session has expired|login.*refresh'
    if ($_.Exception.Message -notmatch $credentialErrors)
    {
        throw
    }

    Write-Host 'AWS sign-in is required. Choose the spideynet-deployer user in your browser.'
    & aws login --profile $settings.Profile --region $settings.Region
    if ($LASTEXITCODE -ne 0)
    {
        throw 'AWS sign-in failed.'
    }

    $identity = Invoke-ServerAws -Settings $settings -Arguments @('sts', 'get-caller-identity', '--output', 'json') | ConvertFrom-Json
}

if ($identity.Account -ne $settings.AccountId)
{
    throw "Wrong AWS account: expected $($settings.AccountId), received $($identity.Account). No resources were changed."
}

if ($Action -eq 'Stop')
{
    & (Join-Path $PSScriptRoot 'Remove-EcsServer.ps1') -Settings $settings
    return
}

# Start Docker Desktop when needed and reject Windows-container engines before provisioning.
Get-Command docker -ErrorAction Stop | Out-Null
$engine = & docker info --format '{{.OSType}}' 2>$null
if ($LASTEXITCODE -ne 0)
{
    Write-Host 'Starting Docker Desktop...'
    & docker desktop start
    if ($LASTEXITCODE -ne 0)
    {
        throw 'Docker Desktop could not start. Start it manually and retry.'
    }

    $deadline = [DateTime]::UtcNow.AddMinutes(2)
    do
    {
        $engine = & docker info --format '{{.OSType}}' 2>$null
        if ($LASTEXITCODE -eq 0)
        {
            break
        }

        Start-Sleep -Seconds 2
    } while ([DateTime]::UtcNow -lt $deadline)
}

if ($LASTEXITCODE -ne 0 -or $engine -ne 'linux')
{
    throw 'Docker must be running with Linux containers.'
}

if ($Action -eq 'Update')
{
    Write-Host 'Building and uploading the current source before replacing the existing server. Sessions reset during deployment.'
}
else
{
    Write-Host 'Building and uploading the current source. Existing sessions will be interrupted.'
}
& (Join-Path $PSScriptRoot 'Deploy-Ecs.ps1') -VpcId $settings.VpcId -SubnetId $settings.SubnetId `
    -Region $settings.Region -StackName $settings.StackName -Profile $settings.Profile -BaseImages $BaseImages `
    -UpdateOnly:($Action -eq 'Update')

$endpoint = & (Join-Path $PSScriptRoot 'Get-EcsEndpoint.ps1') -Region $settings.Region `
    -StackName $settings.StackName -Profile $settings.Profile -Output Object

# ECS readiness precedes certificate issuance; verify public HTTPS before reporting success.
Write-Host 'Waiting for trusted HTTPS...'
$deadline = [DateTime]::UtcNow.AddMinutes(3)
do
{
    try
    {
        $health = Invoke-RestMethod -Uri $endpoint.Health -TimeoutSec 10
        if ($null -ne $health.protocol)
        {
            Write-Host "Server ready: $($endpoint.Relay) (protocol $($health.protocol))"
            Write-Host "Health: $($endpoint.Health)"
            return
        }
    }
    catch
    {
        $lastError = $_.Exception.Message
    }

    Start-Sleep -Seconds 5
} while ([DateTime]::UtcNow -lt $deadline)

throw "The server was deployed but HTTPS is not ready: $lastError. Check $($endpoint.LogGroup). Resources remain running; use Stop-Server.bat to remove them."
