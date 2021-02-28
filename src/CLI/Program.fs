open System
open IPCSocketBridge

[<IPCMethod("yummy")>]
let testFunc a b = a + b

[<IPCMethod>]
let testFunc2 a b = a + b

[<EntryPoint>]
let main argv =
    let mappyBoi = CollectMethods.getMethods ()
    printfn "%A" mappyBoi
    0 // return an integer exit code
