param(
    [Parameter(Mandatory)]
    [string]$VpcId,

    [Parameter(Mandatory)]
    [string]$SubnetId,

    [string]$Region = 'ap-southeast-2',
    [string]$StackName = 'spideynet-ecs',
    [string]$Profile = 'spideynet',

    [ValidateSet('Refresh', 'Cached')]
    [string]$BaseImages = 'Refresh'
)

$ErrorActionPreference = 'Stop'
$script:AwsOptions = @('--region', $Region, '--profile', $Profile, '--no-cli-pager')
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$imageTag = 'build-' + [DateTime]::UtcNow.ToString('yyyyMMddHHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)

function Invoke-Aws
{
    param([string[]]$Arguments)

    $output = & aws @Arguments @script:AwsOptions
    if ($LASTEXITCODE -ne 0)
    {
        throw "AWS command failed: $($Arguments[0]) $($Arguments[1])"
    }

    return $output
}

function Invoke-Docker
{
    param([string[]]$Arguments)

    for ($attempt = 1; $attempt -le 3; $attempt++)
    {
        # Preserve live build output and inspect stderr consistently in both PowerShell versions.
        $lines = [Collections.Generic.List[string]]::new()
        $previousPreference = $ErrorActionPreference
        try
        {
            $ErrorActionPreference = 'Continue'
            & docker @Arguments 2>&1 | ForEach-Object {
                $line = $_.ToString()
                $lines.Add($line)
                Write-Host $line
            }
            $exitCode = $LASTEXITCODE
        }
        finally
        {
            $ErrorActionPreference = $previousPreference
        }

        if ($exitCode -eq 0)
        {
            return
        }

        # Retry interrupted registry requests, without retrying compilation or upload failures.
        $output = $lines -join "`n"
        $registryFailure = $Arguments[0] -eq 'build' -and
            $output -match 'failed to resolve source metadata|failed to fetch.*token|failed to do request' -and
            $output -match '\bEOF\b|connection reset|TLS handshake timeout|i/o timeout|temporary failure|unexpected status.*\b(429|500|502|503|504)\b'
        if ($registryFailure -and $attempt -lt 3)
        {
            Write-Host "Image registry connection failed. Retrying build ($($attempt + 1)/3)..."
            Start-Sleep -Seconds ($attempt * 2)
            continue
        }

        $hint = if ($registryFailure)
        {
            ' The image registry is unavailable. Try Run-Server.bat -BaseImages Cached to use available Docker build cache while rebuilding the current source. If the base images are not cached, restore registry connectivity and retry.'
        }
        else
        {
            ''
        }

        throw "Docker command failed: $($Arguments[0]) (exit $exitCode).$hint"
    }
}

function Deploy-Stack
{
    param([int]$DesiredCount)

    # Zero tasks bootstraps the private registries before the first image upload.
    Invoke-Aws -Arguments @(
        'cloudformation', 'deploy', '--stack-name', $StackName,
        '--template-file', (Join-Path $PSScriptRoot 'stack.yaml'),
        '--capabilities', 'CAPABILITY_NAMED_IAM', '--no-fail-on-empty-changeset',
        '--parameter-overrides', "VpcId=$VpcId", "SubnetId=$SubnetId",
        "ImageTag=$imageTag", "DesiredCount=$DesiredCount", "ExecutionBoundaryArn=$executionBoundary"
    ) | Out-Host
}

# Validate the identity and public routing before creating billable resources.
$identity = (Invoke-Aws -Arguments @('sts', 'get-caller-identity', '--output', 'json')) | ConvertFrom-Json
Write-Host "Deploying to AWS account $($identity.Account), region $Region."
$executionBoundary = "arn:aws:iam::$($identity.Account):policy/SpideyNetEcsExecutionBoundary"

$subnet = (Invoke-Aws -Arguments @('ec2', 'describe-subnets', '--subnet-ids', $SubnetId, '--output', 'json')) | ConvertFrom-Json
if ($subnet.Subnets.Count -ne 1 -or $subnet.Subnets[0].VpcId -ne $VpcId)
{
    throw 'The subnet must belong to the selected VPC.'
}

$routes = (Invoke-Aws -Arguments @('ec2', 'describe-route-tables', '--filters', "Name=vpc-id,Values=$VpcId", '--output', 'json')) | ConvertFrom-Json
$table = @($routes.RouteTables | Where-Object { $_.Associations.SubnetId -contains $SubnetId })
if ($table.Count -eq 0)
{
    $table = @($routes.RouteTables | Where-Object { $_.Associations.Main -contains $true })
}

$publicRoute = @($table.Routes | Where-Object {
    $_.DestinationCidrBlock -eq '0.0.0.0/0' -and $_.GatewayId -like 'igw-*' -and $_.State -eq 'active'
})
if ($publicRoute.Count -eq 0)
{
    throw 'Use a public subnet with a direct default route to an internet gateway, not a NAT gateway.'
}

Invoke-Docker -Arguments @('info', '--format', '{{.OSType}}')
Deploy-Stack -DesiredCount 0

$stack = (Invoke-Aws -Arguments @('cloudformation', 'describe-stacks', '--stack-name', $StackName, '--output', 'json')) | ConvertFrom-Json
$outputs = @{}
foreach ($entry in $stack.Stacks[0].Outputs)
{
    $outputs[$entry.OutputKey] = $entry.OutputValue
}

$registry = $outputs.RelayRepositoryUri.Split('/')[0]
$relayImage = "$($outputs.RelayRepositoryUri):$imageTag"
$caddyImage = "$($outputs.CaddyRepositoryUri):$imageTag"

$buildOptions = @('build', '--platform', 'linux/amd64')
if ($BaseImages -eq 'Refresh')
{
    $buildOptions += '--pull'
}

Invoke-Docker -Arguments ($buildOptions + @('-t', $relayImage, $projectRoot))
Invoke-Docker -Arguments ($buildOptions + @('-f', (Join-Path $PSScriptRoot 'Dockerfile.caddy'), '-t', $caddyImage, $PSScriptRoot))

# Keep the registry token out of command arguments and the desktop credential store.
$password = Invoke-Aws -Arguments @('ecr', 'get-login-password')
$dockerHost = & docker context inspect --format '{{.Endpoints.docker.Host}}'
if ($LASTEXITCODE -ne 0)
{
    throw 'Could not inspect the Docker endpoint.'
}

# Isolate the short-lived registry login from desktop credential helpers.
$authDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ('spideynet-docker-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $authDirectory | Out-Null
$windowsIdentity = [System.Security.Principal.WindowsIdentity]::GetCurrent()
$access = [System.Security.AccessControl.DirectorySecurity]::new()
$access.SetOwner($windowsIdentity.User)
$access.SetAccessRuleProtection($true, $false)
$access.AddAccessRule([System.Security.AccessControl.FileSystemAccessRule]::new(
    $windowsIdentity.User, 'FullControl', 'ContainerInherit, ObjectInherit', 'None', 'Allow'))
Set-Acl -LiteralPath $authDirectory -AclObject $access

try
{
    # An auth entry also prevents Docker from auto-selecting the Windows helper.
    $basicAuth = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("AWS:$password"))
    $authConfig = @{ auths = @{ $registry = @{ auth = $basicAuth } } } | ConvertTo-Json -Depth 4
    [IO.File]::WriteAllText((Join-Path $authDirectory 'config.json'), $authConfig)

    Invoke-Docker -Arguments @('--config', $authDirectory, '--host', $dockerHost, 'push', $relayImage)
    Invoke-Docker -Arguments @('--config', $authDirectory, '--host', $dockerHost, 'push', $caddyImage)
}
finally
{
    $password = $null
    $basicAuth = $null
    $authConfig = $null
    & docker --config $authDirectory logout $registry | Out-Host
    $authFile = Join-Path $authDirectory 'config.json'
    if (Test-Path -LiteralPath $authFile)
    {
        Remove-Item -LiteralPath $authFile
    }
    Remove-Item -LiteralPath $authDirectory
}

Deploy-Stack -DesiredCount 1
& (Join-Path $PSScriptRoot 'Get-EcsEndpoint.ps1') -Region $Region -StackName $StackName -Profile $Profile
