#!/usr/bin/env pwsh

[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]
    $SubscriptionName,

    [Parameter(Mandatory = $true)]
    [string]
    $ResourceGroupName,

    [Parameter(Mandatory = $true)]
    [string]
    $VmName,

    [string]
    $Location = 'West Central US'
)

$ErrorActionPreference = 'Stop'

Set-AzContext -Subscription $SubscriptionName | Out-Null

New-AzResourceGroup -Name $ResourceGroupName -Location $Location | Out-Null

$sshPublicKey = Get-AzKeyVaultSecret -VaultName 'functions-crank-kv' -Name 'LinuxCrankAgentVmSshKey-Public' |
                    ForEach-Object SecretValue

New-AzResourceGroupDeployment -ResourceGroupName $ResourceGroupName -TemplateFile .\template.json `
    -TemplateParameterObject @{
        vmName = $VmName
        adminUserName = 'Functions'
        authenticationType = 'sshPublicKey'
        adminPasswordOrKey = $sshPublicKey
        dnsLabelPrefix = $VmName
        VmSize = 'Standard_E2s_v3'
    }
