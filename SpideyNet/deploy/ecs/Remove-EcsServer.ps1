param(
    [Parameter(Mandatory)]$Settings
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'Server-Aws.psm1') -Force
$stackName = $Settings.StackName
$repositoryNames = @("$stackName-relay", "$stackName-caddy")
$logGroup = "/spideynet/$stackName"

# Stop the named service even if an interrupted stack deletion left it orphaned.
$tasks = Invoke-ServerAws -Settings $Settings -Arguments @(
    'ecs', 'list-tasks', '--cluster', $stackName,
    '--service-name', "$stackName-relay", '--desired-status', 'RUNNING', '--output', 'json'
) -MissingPattern 'ClusterNotFoundException|ServiceNotFoundException'

Invoke-ServerAws -Settings $Settings -Arguments @(
    'ecs', 'update-service', '--cluster', $stackName,
    '--service', "$stackName-relay", '--desired-count', '0', '--output', 'json'
) -MissingPattern 'ServiceNotFoundException|ClusterNotFoundException' | Out-Null

if ($tasks)
{
    $taskIds = @((ConvertFrom-Json $tasks).taskArns)
    if ($taskIds.Count -gt 0)
    {
        Write-Host 'Waiting for the server task to stop...'
        Invoke-ServerAws -Settings $Settings -Arguments (@(
            'ecs', 'wait', 'tasks-stopped', '--cluster', $stackName, '--tasks'
        ) + $taskIds) | Out-Null
    }
}

# A missing stack is valid on a repeated cleanup; access failures must not be ignored.
$stack = Invoke-ServerAws -Settings $Settings -Arguments @(
    'cloudformation', 'describe-stacks', '--stack-name', $stackName, '--output', 'json'
) -MissingPattern 'ValidationError[\s\S]*Stack with id .+ does not exist'

if ($stack)
{
    $details = ($stack | ConvertFrom-Json).Stacks[0]
    if ($details.StackStatus -match '_IN_PROGRESS$' -and $details.StackStatus -ne 'DELETE_IN_PROGRESS')
    {
        throw "Stack operation $($details.StackStatus) is still running. Wait for it to finish, then retry Stop-Server.bat."
    }

    if ($details.StackStatus -ne 'DELETE_IN_PROGRESS')
    {
        Write-Host 'Deleting the ECS deployment and its log group...'
        Invoke-ServerAws -Settings $Settings -Arguments @(
            'cloudformation', 'delete-stack', '--stack-name', $details.StackId
        ) | Out-Null
    }

    try
    {
        Invoke-ServerAws -Settings $Settings -Arguments @(
            'cloudformation', 'wait', 'stack-delete-complete', '--stack-name', $details.StackId
        ) | Out-Null
    }
    catch
    {
        $waitFailure = $_.Exception.Message

        # The waiter hides the cause; retrieve resource reasons using existing read permissions.
        try
        {
            $resources = Invoke-ServerAws -Settings $Settings -Arguments @(
                'cloudformation', 'describe-stack-resources', '--stack-name', $details.StackId,
                '--output', 'json'
            ) | ConvertFrom-Json
            $reasons = @($resources.StackResources | Where-Object { $_.ResourceStatus -eq 'DELETE_FAILED' } |
                ForEach-Object { "$($_.LogicalResourceId): $($_.ResourceStatusReason)" })
            if ($reasons.Count -gt 0)
            {
                $waitFailure += "`nFailed resources:`n" + ($reasons -join "`n")
            }
        }
        catch
        {
            $waitFailure += "`nCould not retrieve resource details: $($_.Exception.Message)"
        }

        throw "$waitFailure`nLogs and image repositories have not been cleaned up. Correct the reported failure, then retry Stop-Server.bat."
    }
}

# The template retains registries, so remove them explicitly only after tasks stop.
foreach ($name in $repositoryNames)
{
    Write-Host "Deleting image repository $name..."
    Invoke-ServerAws -Settings $Settings -Arguments @(
        'ecr', 'delete-repository', '--repository-name', $name, '--force'
    ) -MissingPattern 'RepositoryNotFoundException' | Out-Null
}

# Also handle an orphaned log group from a partially removed deployment.
Invoke-ServerAws -Settings $Settings -Arguments @(
    'logs', 'delete-log-group', '--log-group-name', $logGroup
) -MissingPattern 'ResourceNotFoundException' | Out-Null

Write-Host 'ECS deployment, logs, and image repositories removed. No further compute, public-IP, image-storage, or log-storage charges from these resources.'
Write-Host 'Other AWS resources, including the old EC2 instance and its disk, are unchanged.'
