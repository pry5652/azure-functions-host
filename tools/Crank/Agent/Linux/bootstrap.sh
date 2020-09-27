#!/bin/bash

git config --global core.autocrlf input
mkdir /home/Functions/github
cd /home/Functions/github
git clone https://github.com/Azure/azure-functions-host.git
cd azure-functions-host
git checkout anatolib/crank-linux-container

cd tools/Crank/Agent
echo Updated bootstrap.sh
sudo find . -name "*.sh" -exec sudo chmod +x {} \;
sudo find . -name "*.ps1" -exec sudo chmod +x {} \;
Linux/install-powershell.sh
sudo -H -u Functions ./setup-crank-agent-json.ps1 -ParametersJson $1
