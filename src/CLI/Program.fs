open System
open IPCSocketBridge

[<IPCMethod("yummy")>]
let testFunc a b = a + b

[<IPCMethod>]
let testFunc2 a b = a + b

[<EntryPoint>]
let main argv =
    let declaration = CollectMethods.getMethods ()
    printfn "%A" declaration

    printfn "%s" "***********************************"

    let externDec = Declaration.exportDeclaration declaration
    printfn "%A" externDec
    0 // return an integer exit code
