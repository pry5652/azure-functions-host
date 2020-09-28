#!/bin/bash

echo --- Cloning azure-functions-host repo
mkdir /home/Functions/github
cd /home/Functions/github
git clone https://github.com/Azure/azure-functions-host.git
cd azure-functions-host
git checkout anatolib/crank-linux-container

echo --- Fixing script files format and permissions
cd tools/Crank/Agent
sudo find . -name "*.sh" -exec sudo chmod +xr {} \;
sudo find . -name "*.ps1" -exec sudo chmod +xr {} \;

sudo apt-get update
sudo apt-get install -y dos2unix
sudo find . -name "*.sh" -exec dos2unix {} \;
sudo find . -name "*.ps1" -exec dos2unix {} \;

Linux/install-powershell.sh

echo --- Invoking setup-crank-agent-json.ps1 with parameters: $1
./setup-crank-agent-json.ps1 -ParametersJson $1 -Verbose
