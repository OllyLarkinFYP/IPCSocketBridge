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
    dotnet run --export $DIR_PATH"

let rec parseArgs =
    function
    | [] -> ()
    | "--export"::path::lst -> 
        export path
        parseArgs lst
    | "--validate"::path::lst ->
        let dec = 
            File.ReadAllText(path)
            |> JsonConvert.DeserializeObject<ExternalDeclaration>
        let decManager = DeclarationManager(dec)
        parseArgs lst
    | _ -> printfn "%s" help

[<EntryPoint>]
let main argv =
    argv
    |> Array.toList
    |> parseArgs
    |> ignore
    0 // return an integer exit code
