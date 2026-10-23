function Invoke-ServerAws
{
    param(
        [Parameter(Mandatory)]$Settings,
        [Parameter(Mandatory)][string[]]$Arguments,
        [string]$MissingPattern
    )

    # Keep command errors separate from JSON; only explicit not-found errors are idempotent.
    $previousPreference = $ErrorActionPreference
    try
    {
        # Windows PowerShell wraps native stderr as ErrorRecord objects.
        $ErrorActionPreference = 'Continue'
        $result = & aws @Arguments --profile $Settings.Profile --region $Settings.Region --no-cli-pager 2>&1
        $code = $LASTEXITCODE
    }
    finally
    {
        $ErrorActionPreference = $previousPreference
    }
    $text = ($result | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine
    if ($code -eq 0)
    {
        return $text
    }

    if ($MissingPattern -and $text -match $MissingPattern)
    {
        return $null
    }

    throw "AWS $($Arguments[0]) $($Arguments[1]) failed: $text"
}

Export-ModuleMember -Function Invoke-ServerAws
