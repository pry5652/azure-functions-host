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

New-AzResourceGroupDeployment -ResourceGroupName $ResourceGroupName -TemplateFile .\template.json `
    -TemplateParameterObject @{
        vmName = 'functions-crank-test'
        adminUserName = 'Functions'
        authenticationType = 'sshPublicKey'
        adminPasswordOrKey = TODO
        dnsLabelPrefix = 'functions-crank-test'
        location = $Location
        VmSize = 'Standard_E2s_v3'
    }
