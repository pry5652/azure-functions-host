open Farmer
open Farmer.Builders

let myVm = vm {
    name "functions-crank-linux-test"
    username "Functions"
    vm_size Vm.Standard_E2s_v3
    operating_system Vm.WindowsServer_2012Datacenter
    os_disk 128 Vm.Premium_LRS
}

let deployment = arm {
    location Location.WestCentralUS
    add_resource myVm
}

deployment
|> Writer.quickWrite "template"
