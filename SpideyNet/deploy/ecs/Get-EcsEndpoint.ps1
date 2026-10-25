param(
    [string]$Region = 'ap-southeast-2',
    [string]$StackName = 'spideynet-ecs',
    [string]$Profile = 'spideynet',

    [ValidateSet('Display', 'Object')]
    [string]$Output = 'Display'
)

$ErrorActionPreference = 'Stop'
$script:AwsOptions = @('--region', $Region, '--profile', $Profile, '--no-cli-pager')

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

$stack = (Invoke-Aws -Arguments @('cloudformation', 'describe-stacks', '--stack-name', $StackName, '--output', 'json')) | ConvertFrom-Json
$outputs = @{}
foreach ($entry in $stack.Stacks[0].Outputs)
{
    $outputs[$entry.OutputKey] = $entry.OutputValue
}

$tasks = (Invoke-Aws -Arguments @('ecs', 'list-tasks', '--cluster', $outputs.ClusterName, '--service-name', $outputs.ServiceName, '--desired-status', 'RUNNING', '--output', 'json')) | ConvertFrom-Json
if ($tasks.TaskArns.Count -ne 1)
{
    throw "Expected one running task. Inspect ECS service events and log group $($outputs.LogGroup)."
}

$task = (Invoke-Aws -Arguments @('ecs', 'describe-tasks', '--cluster', $outputs.ClusterName, '--tasks', $tasks.TaskArns[0], '--output', 'json')) | ConvertFrom-Json
$interfaceId = ($task.Tasks[0].Attachments.Details | Where-Object Name -eq 'networkInterfaceId').Value
if (!$interfaceId)
{
    throw 'The task network interface is not ready yet.'
}

$network = (Invoke-Aws -Arguments @('ec2', 'describe-network-interfaces', '--network-interface-ids', $interfaceId, '--output', 'json')) | ConvertFrom-Json
$address = $network.NetworkInterfaces[0].Association.PublicIp
if (!$address)
{
    throw 'The task has no public IPv4 address.'
}

if ($Output -eq 'Object')
{
    return [pscustomobject]@{
        Relay = "wss://$address"
        Health = "https://$address/health"
        LogGroup = $outputs.LogGroup
    }
}

Write-Host "Relay: wss://$address"
Write-Host "Health: https://$address/health"
Write-Host "CloudWatch log group: $($outputs.LogGroup)"
