open System
open IPCSocketBridge

[<EntryPoint>]
let main argv =
    let message = Say.hello "mate"
    printfn "%s" message
    0 // return an integer exit code
