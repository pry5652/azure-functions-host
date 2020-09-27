#!/bin/bash

mkdir /home/Functions/github
cd /home/Functions/github
git clone https://github.com/Azure/azure-functions-host.git
cd azure-functions-host
git checkout anatolib/crank-linux-container

cd tools/Crank/Agent
sudo find . -name "*.sh" -exec chmod +x {} \;
sudo find . -name "*.ps1" -exec chmod +x {} \;
Linux/install-powershell.sh
sudo -H -u Functions ./setup-crank-agent-json.ps1 -ParametersJson $1
