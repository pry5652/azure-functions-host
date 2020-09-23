#!/bin/bash

mkdir /home/Functions/github
cd /home/Functions/github
git clone https://github.com/Azure/azure-functions-host.git
git checkout anatolib/crank-agent-setup

cd azure-functions-host/tools/Crank/Agent
./install-powershell.sh
./setup-crank-agent.ps1 -CrankBranch dev
