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
    $VmSize = 'Standard_E2s_v3',

    [string]
    $Location = 'West Central US'
)

$ErrorActionPreference = 'Stop'

Set-AzContext -Subscription $SubscriptionName | Out-Null

New-AzResourceGroup -Name $ResourceGroupName -Location $Location | Out-Null

$vaultSubscriptionId = (Get-AzSubscription -SubscriptionName 'Antares-Demo').Id

New-AzResourceGroupDeployment `
    -ResourceGroupName $ResourceGroupName `
    -TemplateFile .\template.json `
    -TemplateParameterObject @{
        vmName = $VmName
        dnsLabelPrefix = $VmName
        vmSize = $VmSize
        adminUsername = 'Functions'
        authenticationType = 'sshPublicKey'
        vaultName = 'functions-crank-kv'
        vaultResourceGroupName = 'FunctionsCrank'
        vaultSubscription = $vaultSubscriptionId
        secretName = 'LinuxCrankAgentVmSshKey-Public'
    }
