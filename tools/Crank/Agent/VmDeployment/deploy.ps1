#!/usr/bin/env pwsh

[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]
    $SubscriptionName,

    [Parameter(Mandatory = $true)]
    [string]
    $BaseName,

    [string]
    $VmSize = 'Standard_E2s_v3',

    [string]
    $OsDiskType = 'Premium_LRS',

    [string]
    $Location = 'West Central US'
)

$ErrorActionPreference = 'Stop'

$resourceGroupName = "FunctionsCrank-$BaseName"

Set-AzContext -Subscription $SubscriptionName | Out-Null

New-AzResourceGroup -Name $resourceGroupName -Location $Location | Out-Null

$vmName = "functions-crank-$BaseName"
$vaultSubscriptionId = (Get-AzSubscription -SubscriptionName 'Antares-Demo').Id

New-AzResourceGroupDeployment `
    -ResourceGroupName $resourceGroupName `
    -TemplateFile .\template.json `
    -TemplateParameterObject @{
        vmName = $vmName
        dnsLabelPrefix = $vmName
        vmSize = $VmSize
        osDiskType = $OsDiskType
        adminUsername = 'Functions'
        authenticationType = 'sshPublicKey'
        vaultName = 'functions-crank-kv'
        vaultResourceGroupName = 'FunctionsCrank'
        vaultSubscription = $vaultSubscriptionId
        secretName = 'LinuxCrankAgentVmSshKey-Public'
    }
