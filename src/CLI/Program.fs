open System
open IPCSocketBridge
open IPCSocketBridge.Declaration
open System.IO
open Newtonsoft.Json

[<IPCMethod("yummy")>]
let testFunc a b = a + b

[<IPCMethod>]
let testFunc2 a b = a + b

let help =
    "Invalid arguments passed. Correct usage is:
    dotnet run --export $DIR_PATH
    dotnet run --validate $DEC_PATH
    dotnet run --execute $DEC_PATH
    dotnet run $DEC_PATH --connect $PORT"

let rec parseArgsRec =
    function
    | [] -> ()
    | "--export"::path::lst -> 
        export path
        parseArgsRec lst
    | "--validate"::path::lst ->
        let dec = 
            File.ReadAllText(path)
            |> JsonConvert.DeserializeObject<ExternalDeclaration>
        let decManager = DeclarationManager(dec)
        parseArgsRec lst
    | "--execute"::path::lst ->
        let dec = 
            File.ReadAllText(path)
            |> JsonConvert.DeserializeObject<ExternalDeclaration>
        let decManager = DeclarationManager(dec)
        let method = "yummy"
        let parameters = [box 1; box 2] |> List.toArray
        decManager.Execute method parameters |> printfn "%A"
        parseArgsRec lst
    | path::"--connect"::port::lst ->
        let p = int port
        let dec = 
            File.ReadAllText(path)
            |> JsonConvert.DeserializeObject<ExternalDeclaration>
        let ipcManager = IPC.IPCManager(p, dec)
        ipcManager.Start()
        parseArgsRec lst
        
    | _ -> printfn "%s" help

let parseArgs =
    function
    | [] -> printfn "%s" help
    | lst -> parseArgsRec lst 

[<EntryPoint>]
let main argv =
    argv
    |> Array.toList
    |> parseArgs
    |> ignore
    0 // return an integer exit code
