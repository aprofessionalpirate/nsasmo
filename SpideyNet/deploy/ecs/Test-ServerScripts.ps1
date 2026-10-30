$ErrorActionPreference = 'Stop'
$settings = Get-Content (Join-Path $PSScriptRoot 'server-settings.json') -Raw | ConvertFrom-Json
$global:ServerTest = @{
    Calls = [Collections.Generic.List[string]]::new()
    StackExists = $true
    StackStatus = 'CREATE_COMPLETE'
    ServiceExists = $true
    WrongAccount = $false
    DenyDelete = $false
    FailStackWait = $false
    SignedIn = $true
    DockerReady = $true
    FailPush = $false
    BuildFailures = 0
    BuildError = ''
    Repositories = @{ 'spideynet-ecs-relay' = $true; 'spideynet-ecs-caddy' = $true }
    LogsExist = $true
}

function Check
{
    param([bool]$Condition, [string]$Message)

    if (!$Condition)
    {
        throw "Failed: $Message"
    }
}

function global:aws
{
    $arguments = @($args)
    $operation = $arguments[0..1] -join ' '
    $global:ServerTest.Calls.Add('aws ' + ($arguments -join ' '))
    $global:LASTEXITCODE = 0

    switch ($operation)
    {
        'login --profile'
        {
            $global:ServerTest.SignedIn = $true
            return
        }
        'sts get-caller-identity'
        {
            if (!$global:ServerTest.SignedIn)
            {
                $global:LASTEXITCODE = 1
                return 'An error occurred (ExpiredToken) when calling GetCallerIdentity'
            }

            $account = if ($global:ServerTest.WrongAccount)
            {
                '000000000000'
            }
            else
            {
                $settings.AccountId
            }
            return (@{ Account = $account } | ConvertTo-Json)
        }
        'ec2 describe-subnets'
        {
            return (@{ Subnets = @(@{ VpcId = $settings.VpcId }) } | ConvertTo-Json -Depth 5)
        }
        'ec2 describe-route-tables'
        {
            return (@{ RouteTables = @(@{
                Associations = @(@{ Main = $true })
                Routes = @(@{ DestinationCidrBlock = '0.0.0.0/0'; GatewayId = 'igw-test'; State = 'active' })
            }) } | ConvertTo-Json -Depth 8)
        }
        'cloudformation deploy'
        {
            $global:ServerTest.StackExists = $true
            $global:ServerTest.ServiceExists = $true
            $global:ServerTest.LogsExist = $true
            return
        }
        'cloudformation describe-stacks'
        {
            if (!$global:ServerTest.StackExists)
            {
                $global:LASTEXITCODE = 1
                return 'An error occurred (ValidationError) when calling DescribeStacks: Stack with id spideynet-ecs does not exist'
            }

            return (@{ Stacks = @(@{
                StackId = 'arn:aws:cloudformation:ap-southeast-2:123456789012:stack/spideynet-ecs/test'
                StackStatus = $global:ServerTest.StackStatus
                Outputs = @(
                    @{ OutputKey = 'ClusterName'; OutputValue = 'spideynet-ecs' },
                    @{ OutputKey = 'ServiceName'; OutputValue = 'spideynet-ecs-relay' },
                    @{ OutputKey = 'RelayRepositoryUri'; OutputValue = 'example.invalid/spideynet-ecs-relay' },
                    @{ OutputKey = 'CaddyRepositoryUri'; OutputValue = 'example.invalid/spideynet-ecs-caddy' },
                    @{ OutputKey = 'LogGroup'; OutputValue = '/spideynet/spideynet-ecs' }
                )
            }) } | ConvertTo-Json -Depth 8)
        }
        'cloudformation delete-stack'
        {
            if ($global:ServerTest.DenyDelete)
            {
                $global:LASTEXITCODE = 1
                return 'An error occurred (AccessDenied) when calling DeleteStack'
            }

            $global:ServerTest.StackExists = $false
            $global:ServerTest.ServiceExists = $false
            $global:ServerTest.LogsExist = $false
            return
        }
        'ecs list-tasks'
        {
            if (!$global:ServerTest.ServiceExists)
            {
                $global:LASTEXITCODE = 1
                return 'An error occurred (ClusterNotFoundException) when calling ListTasks'
            }

            return '{"taskArns":["test-task"]}'
        }
        'ecs update-service'
        {
            if (!$global:ServerTest.ServiceExists)
            {
                $global:LASTEXITCODE = 1
                return 'An error occurred (ClusterNotFoundException) when calling UpdateService'
            }

            return '{}'
        }
        'ecs describe-tasks'
        {
            return '{"tasks":[{"attachments":[{"details":[{"name":"networkInterfaceId","value":"eni-test"}]}]}]}'
        }
        'ec2 describe-network-interfaces'
        {
            return '{"NetworkInterfaces":[{"Association":{"PublicIp":"203.0.113.10"}}]}'
        }
        'ecr get-login-password'
        {
            return 'fake-test-token'
        }
        'ecr delete-repository'
        {
            if (!$global:ServerTest.StackExists -and !$global:ServerTest.ServiceExists)
            {
                $name = $arguments[[Array]::IndexOf($arguments, '--repository-name') + 1]
                if (!$global:ServerTest.Repositories[$name])
                {
                    $global:LASTEXITCODE = 1
                    return 'An error occurred (RepositoryNotFoundException) when calling DeleteRepository'
                }

                $global:ServerTest.Repositories[$name] = $false
                return '{}'
            }

            throw 'Images must not be deleted before the server stops.'
        }
        'cloudformation wait'
        {
            if ($global:ServerTest.FailStackWait)
            {
                $global:LASTEXITCODE = 1
                return 'Waiter StackDeleteComplete failed: DELETE_FAILED'
            }

            return
        }
        'cloudformation describe-stack-resources'
        {
            return '{"StackResources":[{"LogicalResourceId":"Task","ResourceStatus":"DELETE_FAILED","ResourceStatusReason":"AccessDenied: ecs:DeregisterTaskDefinition on resource: *"}]}'
        }
        'ecs wait' { return }
        'logs delete-log-group'
        {
            if (!$global:ServerTest.LogsExist)
            {
                $global:LASTEXITCODE = 1
                return 'An error occurred (ResourceNotFoundException) when calling DeleteLogGroup'
            }

            $global:ServerTest.LogsExist = $false
            return
        }
        default { throw "Unexpected AWS operation: $operation" }
    }
}

function global:docker
{
    $arguments = @($args)
    $global:ServerTest.Calls.Add('docker ' + ($arguments -join ' '))
    $global:LASTEXITCODE = 0
    if ($arguments[0] -eq 'build' -and $global:ServerTest.BuildFailures -gt 0)
    {
        $global:ServerTest.BuildFailures--
        $global:LASTEXITCODE = 1
        return $global:ServerTest.BuildError
    }

    if ($global:ServerTest.FailPush -and $arguments -contains 'push')
    {
        $global:LASTEXITCODE = 1
        return
    }
    if ($arguments[0] -eq 'info')
    {
        if (!$global:ServerTest.DockerReady)
        {
            $global:LASTEXITCODE = 1
            return
        }

        return 'linux'
    }

    if (($arguments[0..1] -join ' ') -eq 'desktop start')
    {
        $global:ServerTest.DockerReady = $true
    }

    if ($arguments[0] -eq 'context')
    {
        return 'npipe:////./pipe/mock'
    }
}

function global:Invoke-RestMethod
{
    param($Uri, $TimeoutSec)
    Check ($Uri -eq 'https://203.0.113.10/health') 'Validate the discovered HTTPS endpoint'
    return @{ protocol = 5 }
}

try
{
    # These ECS APIs reject resource-scoped permissions, even for this task family.
    $policy = Get-Content (Join-Path $PSScriptRoot 'deployer-policy.json') -Raw | ConvertFrom-Json
    foreach ($action in @('ecs:DescribeTaskDefinition', 'ecs:DeregisterTaskDefinition'))
    {
        $grants = @($policy.Statement | Where-Object {
            $_.Effect -eq 'Allow' -and @($_.Action) -contains $action -and $_.Resource -eq '*' -and
            $_.Condition.StringEquals.'aws:RequestedRegion' -eq $settings.Region
        })
        Check ($grants.Count -eq 1) "Grant $action with AWS-supported scope and a region restriction"
    }

    # Mock all external commands: these checks cannot mutate AWS or build containers.
    & (Join-Path $PSScriptRoot 'Manage-Server.ps1') -Action Run
    $firstDeployment = @($global:ServerTest.Calls | Where-Object { $_ -match 'ImageTag=build-' })[0]
    $authFiles = @($global:ServerTest.Calls | Where-Object { $_ -match '^docker --config' })
    Check ($authFiles.Count -eq 3) 'Upload both images and clear the temporary registry login'
    $authDirectory = ($authFiles[0] -split ' --host ')[0] -replace '^docker --config ', ''
    Check (!(Test-Path -LiteralPath $authDirectory)) 'Remove temporary Docker credentials'
    Check (@($global:ServerTest.Calls | Where-Object { $_ -match '^docker build .*--pull' }).Count -eq 2) 'Build both images from current source'
    $sourceRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
    Check (@($global:ServerTest.Calls | Where-Object { $_ -like "docker build *$sourceRoot" }).Count -eq 1) 'Build the relay working tree rather than a previous executable'

    $global:ServerTest.Calls.Clear()
    $global:ServerTest.SignedIn = $false
    $global:ServerTest.DockerReady = $false
    & (Join-Path $PSScriptRoot 'Manage-Server.ps1') -Action Run -BaseImages Cached
    $secondDeployment = @($global:ServerTest.Calls | Where-Object { $_ -match 'ImageTag=build-' })[0]
    Check ($firstDeployment -ne $secondDeployment) 'Give each run a fresh image tag'
    Check ($global:ServerTest.SignedIn -and $global:ServerTest.DockerReady) 'Sign in and start Docker when needed'

    # Updates upload fresh source before a single deployment, without the Run bootstrap/stop step.
    $global:ServerTest.Calls.Clear()
    & (Join-Path $PSScriptRoot 'Manage-Server.ps1') -Action Update -BaseImages Cached
    $calls = $global:ServerTest.Calls
    $deployments = @($calls | Where-Object { $_ -match '^aws cloudformation deploy ' })
    Check ($deployments.Count -eq 1 -and $deployments[0] -match 'DesiredCount=1') 'Update an existing server without scaling to zero'
    Check ($deployments[0] -ne $secondDeployment) 'Give updates a fresh immutable image tag'
    Check (@($calls | Where-Object { $_ -match '^docker build ' }).Count -eq 2) 'Rebuild relay and proxy for the shared image tag'
    Check (@($calls | Where-Object { $_ -match '^docker build .*--pull' }).Count -eq 0) 'Forward cached base-image selection to updates'
    $uploads = @($calls | Where-Object { $_ -match '^docker --config .* push ' })
    Check ($uploads.Count -eq 2 -and $calls.IndexOf($uploads[-1]) -lt $calls.IndexOf($deployments[0])) 'Upload both images before replacing the running task'
    Check (@($calls | Where-Object { $_ -match 'delete-stack|delete-repository|^aws ecs update-service' }).Count -eq 0) 'Preserve the existing stack and repositories during updates'
    foreach ($wrapper in @('Run-Server.bat', 'Update-Server.bat', 'Stop-Server.bat'))
    {
        $batch = Get-Content (Join-Path $PSScriptRoot "../../../$wrapper") -Raw
        $action = $wrapper.Split('-')[0]
        Check (([regex]::Matches($batch, "-Action $action %\*")).Count -eq 2) "Forward settings through both PowerShell launch paths in $wrapper"
        Check ($batch.Contains('if not defined SPIDEYNET_NO_PAUSE pause') -and $batch.Contains('exit /b %SERVER_EXIT_CODE%')) "Preserve pause and failure exit status in $wrapper"
    }

    # Failures before deployment must leave the old server alone.
    foreach ($failure in @('Missing', 'Busy', 'Build', 'Push'))
    {
        $global:ServerTest.Calls.Clear()
        $global:ServerTest.StackExists = $failure -ne 'Missing'
        $global:ServerTest.StackStatus = if ($failure -eq 'Busy') { 'UPDATE_IN_PROGRESS' } else { 'UPDATE_COMPLETE' }
        $global:ServerTest.BuildFailures = if ($failure -eq 'Build') { 1 } else { 0 }
        $global:ServerTest.BuildError = 'error CS1002: ; expected'
        $global:ServerTest.FailPush = $failure -eq 'Push'
        $failed = $false
        try { & (Join-Path $PSScriptRoot 'Manage-Server.ps1') -Action Update }
        catch { $failed = $true }
        Check $failed "Report an update $failure failure"
        Check (@($global:ServerTest.Calls | Where-Object { $_ -match '^aws cloudformation deploy |^aws ecs update-service|delete-stack|delete-repository' }).Count -eq 0) "Leave the running deployment intact after an update $failure failure"
        if ($failure -in @('Missing', 'Busy'))
        {
            Check (@($global:ServerTest.Calls | Where-Object { $_ -match '^docker build ' }).Count -eq 0) 'Reject missing or busy stacks before building'
        }
    }
    $global:ServerTest.StackExists = $true
    $global:ServerTest.StackStatus = 'UPDATE_COMPLETE'
    $global:ServerTest.FailPush = $false

    $global:ServerTest.Calls.Clear()
    & (Join-Path $PSScriptRoot 'Manage-Server.ps1') -Action Stop
    $calls = $global:ServerTest.Calls
    $stopped = @($calls | Where-Object { $_ -match '^aws ecs wait tasks-stopped' })[0]
    $deleted = @($calls | Where-Object { $_ -match '^aws cloudformation wait stack-delete-complete' })[0]
    $images = @($calls | Where-Object { $_ -match '^aws ecr delete-repository' })
    Check ($images.Count -eq 2) 'Remove both retained repositories'
    Check ($calls.IndexOf($stopped) -lt $calls.IndexOf($deleted)) 'Wait for tasks before stack deletion completes'
    Check ($calls.IndexOf($deleted) -lt $calls.IndexOf($images[0])) 'Wait for stack deletion before removing images'
    Check (@($calls | Where-Object { $_ -match '^aws logs delete-log-group' }).Count -eq 1) 'Remove remaining logs'

    # Repeated cleanup and authorization failures have different outcomes.
    & (Join-Path $PSScriptRoot 'Manage-Server.ps1') -Action Stop
    $global:ServerTest.Calls.Clear()
    $global:ServerTest.StackExists = $true
    $global:ServerTest.ServiceExists = $true
    $global:ServerTest.DenyDelete = $true
    $failed = $false
    try { & (Join-Path $PSScriptRoot 'Manage-Server.ps1') -Action Stop }
    catch { $failed = $_.Exception.Message -match 'AccessDenied' }
    Check $failed 'Report permission failures instead of claiming successful cleanup'
    Check (@($global:ServerTest.Calls | Where-Object { $_ -match '^aws ecr delete-repository' }).Count -eq 0) 'Keep images if stack removal fails'

    # A waiter error must include the failed resource and AWS permission reason.
    $global:ServerTest.DenyDelete = $false
    $global:ServerTest.FailStackWait = $true
    $failed = $false
    try { & (Join-Path $PSScriptRoot 'Manage-Server.ps1') -Action Stop }
    catch { $failed = $_.Exception.Message -match 'Task:.*ecs:DeregisterTaskDefinition' }
    Check $failed 'Explain the resource failure hidden behind DELETE_FAILED'
    $global:ServerTest.FailStackWait = $false

    # Registry disconnects may recover; source compilation errors must fail immediately.
    $global:ServerTest.Calls.Clear()
    $global:ServerTest.BuildFailures = 2
    $global:ServerTest.BuildError = 'failed to resolve source metadata: failed to do request: Head https://mcr.microsoft.com/v2/dotnet/sdk/manifests/10.0: EOF'
    & (Join-Path $PSScriptRoot 'Manage-Server.ps1') -Action Run
    Check (@($global:ServerTest.Calls | Where-Object { $_ -match '^docker build ' }).Count -eq 4) 'Retry temporary registry failures before building the proxy'

    $global:ServerTest.Calls.Clear()
    $global:ServerTest.BuildFailures = 4
    $failed = $false
    try { & (Join-Path $PSScriptRoot 'Manage-Server.ps1') -Action Run }
    catch { $failed = $_.Exception.Message -match 'BaseImages Cached' }
    Check $failed 'Explain cached-image recovery after persistent registry failures'
    Check (@($global:ServerTest.Calls | Where-Object { $_ -match '^docker build ' }).Count -eq 3) 'Bound registry retries to three attempts'
    Check (@($global:ServerTest.Calls | Where-Object { $_ -match 'DesiredCount=1| push ' }).Count -eq 0) 'Do not upload or start after a failed build'

    $global:ServerTest.Calls.Clear()
    $global:ServerTest.BuildFailures = 1
    $global:ServerTest.BuildError = 'error CS1002: ; expected'
    $failed = $false
    try { & (Join-Path $PSScriptRoot 'Manage-Server.ps1') -Action Run }
    catch { $failed = $_.Exception.Message -match 'Docker command failed: build' }
    Check $failed 'Report source compilation failures'
    Check (@($global:ServerTest.Calls | Where-Object { $_ -match '^docker build ' }).Count -eq 1) 'Do not retry source compilation failures'

    $global:ServerTest.Calls.Clear()
    $global:ServerTest.FailPush = $true
    $failed = $false
    try { & (Join-Path $PSScriptRoot 'Manage-Server.ps1') -Action Run }
    catch { $failed = $_.Exception.Message -match 'Docker command failed' }
    Check $failed 'Fail startup when upload fails'
    $authCall = @($global:ServerTest.Calls | Where-Object { $_ -match '^docker --config' })[0]
    $authDirectory = ($authCall -split ' --host ')[0] -replace '^docker --config ', ''
    Check (!(Test-Path -LiteralPath $authDirectory)) 'Clear temporary credentials after an upload failure'
    Check (@($global:ServerTest.Calls | Where-Object { $_ -match 'DesiredCount=1' }).Count -eq 0) 'Do not start tasks after an upload failure'

    $global:ServerTest.Calls.Clear()
    $global:ServerTest.WrongAccount = $true
    $failed = $false
    try { & (Join-Path $PSScriptRoot 'Manage-Server.ps1') -Action Stop }
    catch { $failed = $_.Exception.Message -match 'Wrong AWS account' }
    Check $failed 'Reject the wrong AWS account'
    Check ($global:ServerTest.Calls.Count -eq 1) 'Make no resource changes in the wrong account'
    $global:ServerTest.Calls.Clear()
    $failed = $false
    try { & (Join-Path $PSScriptRoot 'Manage-Server.ps1') -Action Update }
    catch { $failed = $_.Exception.Message -match 'Wrong AWS account' }
    Check $failed 'Reject the wrong AWS account during updates'
    Check ($global:ServerTest.Calls.Count -eq 1) 'Do not build or mutate resources for an update in the wrong account'
    Write-Host 'Server lifecycle checks passed.'
}
finally
{
    Remove-Module Server-Aws -ErrorAction SilentlyContinue
    Remove-Item Function:\aws, Function:\docker, Function:\Invoke-RestMethod -ErrorAction SilentlyContinue
    Remove-Variable ServerTest -Scope Global -ErrorAction SilentlyContinue
}
