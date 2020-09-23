#!/bin/bash

mkdir /home/Functions/github
cd /home/Functions/github
git clone https://github.com/Azure/azure-functions-host.git
cd azure-functions-host
git checkout anatolib/crank-agent-setup

cd tools/Crank/Agent
./install-powershell.sh
./setup-crank-agent.ps1 -CrankBranch dev
