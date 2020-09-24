#!/usr/bin/env pwsh

[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]
    $SubscriptionName,

    [Parameter(Mandatory = $true)]
    [string]
    $BaseName,

    [Parameter(Mandatory = $true)]
    [ValidateSet('Linux', 'Windows')]
    $OsType,

    [string]
    $VmSize = 'Standard_E2s_v3',

    [string]
    $OsDiskType = 'Premium_LRS',

    [string]
    $Location = 'West Central US'
)

$ErrorActionPreference = 'Stop'

if ($OsType -ne 'Linux') {
    throw 'Only Linux is supported now'
}

$resourceGroupName = "FunctionsCrank-$OsType-$BaseName"

Set-AzContext -Subscription $SubscriptionName | Out-Null

New-AzResourceGroup -Name $resourceGroupName -Location $Location | Out-Null

$vmName = "functions-crank-$OsType-$BaseName".ToLower()
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
