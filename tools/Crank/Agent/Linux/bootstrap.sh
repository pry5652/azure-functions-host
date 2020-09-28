#!/bin/bash

mkdir /home/Functions/github
cd /home/Functions/github
git clone https://github.com/Azure/azure-functions-host.git
cd azure-functions-host
git checkout anatolib/crank-linux-container

cd tools/Crank/Agent
sudo find . -name "*.sh" -exec sudo chmod +x {} \;
sudo find . -name "*.ps1" -exec sudo chmod +x {} \;

sudo apt update
sudo apt install dos2unix
sudo find . -name "*.sh" -exec dos2unix {} \;
sudo find . -name "*.ps1" -exec dos2unix {} \;

Linux/install-powershell.sh

./setup-crank-agent-json.ps1 -ParametersJson $1
