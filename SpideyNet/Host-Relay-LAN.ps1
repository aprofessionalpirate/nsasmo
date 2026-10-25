param(
    [string]$Address,

    [ValidateRange(1, 65535)]
    [int]$Port = 5000
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Test-PrivateIPv4
{
    param([System.Net.IPAddress]$IPAddress)

    if ($IPAddress.AddressFamily -ne [System.Net.Sockets.AddressFamily]::InterNetwork)
    {
        return $false
    }

    # Match the private IPv4 ranges accepted by the client's ws:// endpoint policy.
    $bytes = $IPAddress.GetAddressBytes()
    return $bytes[0] -eq 10 -or ($bytes[0] -eq 172 -and $bytes[1] -ge 16 -and $bytes[1] -le 31) `
        -or ($bytes[0] -eq 192 -and $bytes[1] -eq 168)
}

try
{
    Get-Command dotnet -ErrorAction Stop | Out-Null

    # Use .NET adapter discovery: no administrator access or CIM service is needed.
    $localAddresses = @(
        foreach ($adapter in [System.Net.NetworkInformation.NetworkInterface]::GetAllNetworkInterfaces())
        {
            if ($adapter.OperationalStatus -ne [System.Net.NetworkInformation.OperationalStatus]::Up)
            {
                continue
            }

            $properties = $adapter.GetIPProperties()
            $hasGateway = @($properties.GatewayAddresses | Where-Object {
                $_.Address.AddressFamily -eq [System.Net.Sockets.AddressFamily]::InterNetwork `
                    -and $_.Address.ToString() -ne '0.0.0.0'
            }).Count -gt 0
            foreach ($unicast in $properties.UnicastAddresses)
            {
                if (Test-PrivateIPv4 $unicast.Address)
                {
                    [pscustomobject]@{
                        Address = $unicast.Address.ToString()
                        Adapter = $adapter.Name
                        HasGateway = $hasGateway
                    }
                }
            }
        }
    )

    if ($Address)
    {
        if (!($localAddresses | Where-Object { $_.Address -eq $Address }))
        {
            throw "Address '$Address' must be a private IPv4 address assigned to an active local adapter."
        }
    }
    else
    {
        # Prefer adapters with a default gateway over virtual or isolated networks.
        $candidates = @($localAddresses | Where-Object { $_.HasGateway })
        if ($candidates.Count -eq 0)
        {
            $candidates = $localAddresses
        }
        if ($candidates.Count -eq 0)
        {
            throw 'No active private IPv4 address found. Connect to your LAN and try again.'
        }

        $selection = 1
        if ($candidates.Count -gt 1)
        {
            Write-Host 'Choose the network your other players use:'
            for ($index = 0; $index -lt $candidates.Count; $index++)
            {
                Write-Host ('  {0}: {1} ({2})' -f ($index + 1), $candidates[$index].Address, $candidates[$index].Adapter)
            }

            $answer = Read-Host 'Network number [1]'
            if ($answer -and (![int]::TryParse($answer, [ref]$selection) `
                -or $selection -lt 1 -or $selection -gt $candidates.Count))
            {
                throw 'Invalid network selection. Run the launcher again and choose a listed number.'
            }
        }
        $Address = $candidates[$selection - 1].Address
    }

    $projectRoot = Split-Path -Parent $PSScriptRoot
    $outputDirectory = Join-Path $projectRoot 'outputs'
    $artifacts = Join-Path $projectRoot 'artifacts/release'
    $project = Join-Path $PSScriptRoot 'Relay/Relay.csproj'

    Write-Host 'Publishing the Release relay (requires the .NET 10 SDK)...'
    & dotnet publish $project -c Release --artifacts-path $artifacts -o $outputDirectory -m:1
    if ($LASTEXITCODE -ne 0)
    {
        exit $LASTEXITCODE
    }

    $listener = 'http://{0}:{1}' -f $Address, $Port
    $connection = 'ws://{0}:{1}' -f $Address, $Port
    $relay = Join-Path $outputDirectory 'Relay.exe'
    Write-Host ''
    Write-Host ('Starting LAN relay on {0}' -f $listener)
    Write-Host ('Health check: {0}/health' -f $listener)
    Write-Host 'Keep this window open. Press Ctrl+C to stop the relay.'
    Write-Host ('If another PC cannot connect, allow inbound TCP {0} on the Windows Firewall Private profile.' -f $Port)
    Write-Host ''
    Write-Host ('Client connection URL: {0}' -f $connection) -ForegroundColor Green
    Write-Host ''

    # Keep the relay in this console so connection logs and Ctrl+C remain available.
    & $relay --urls $listener --contentRoot $outputDirectory
    exit $LASTEXITCODE
}
catch
{
    Write-Host ('LAN relay failed: {0}' -f $_.Exception.Message) -ForegroundColor Red
    exit 1
}
