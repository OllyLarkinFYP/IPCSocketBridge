namespace IPCSocketBridge

open System
open System.Reflection
open System.IO
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open Utils

module Declaration =
    type InternalParam = {
        Name: string
        Type: System.Type
    }

    type ExternalParam = {
        Name: string
        Type: string
    }

    type InternalDeclarationElement = {
        Name: string
        ReturnType: System.Type
        Parameters: InternalParam seq
        Method: MethodInfo
    }
    type InternalDeclaration = InternalDeclarationElement seq

    type ExternalDeclarationElement = {
        Name: string
        ReturnType: string
        Parameters: ExternalParam seq
    }
    type ExternalDeclaration = ExternalDeclarationElement seq

    let convertParams = Seq.map (fun (p: InternalParam) -> {
            ExternalParam.Name=p.Name
            Type=p.Type.ToString()
        })

    let equalDecsElem (inDecElem: InternalDeclarationElement) (exDecElem: ExternalDeclarationElement) =
        inDecElem.Name = exDecElem.Name
        && seqCompare (convertParams inDecElem.Parameters) exDecElem.Parameters
        && inDecElem.ReturnType.ToString() = exDecElem.ReturnType

    let private generateDeclaration (methods: (string * MethodInfo) seq) : InternalDeclaration =
        methods
        |> Seq.map (fun (name, method) -> 
            let parameters = 
                method.GetParameters()
                |> Seq.map (fun param -> { InternalParam.Name = param.Name; Type = param.ParameterType })
            {
                Name = name
                ReturnType = method.ReturnType
                Parameters = parameters
                Method = method
            })

    let private getDeclaration () =
        let mutable names = List.empty
        let validator (name, (method: MethodInfo)) =
            if method.IsGenericMethod || method.ContainsGenericParameters
            then failwithf "Generics are currently not supported for IPC methods: %s, %A" name method
            
            if List.contains name names
            then failwithf "IPC method names must be unique. '%s' was found multiple times. Use the format [<IPCMethod(\"example\")>] to use a different name." name
            else names <- name::names

            name, method

        AppDomain.CurrentDomain.GetAssemblies()
        |> Seq.collect (fun assembly -> assembly.GetTypes())
        |> Seq.collect (fun typ -> typ.GetMethods())
        |> Seq.choose (fun method ->
            method.GetCustomAttribute<IPCMethodAttribute>()
            |> nullableToOption
            |> Option.map (fun attr -> attr.Name, method))
        |> List.ofSeq
        |> List.map validator
        |> generateDeclaration

    let private exportDeclaration : InternalDeclaration -> ExternalDeclaration =
        Seq.map <| fun dec -> {
            Name = dec.Name
            ReturnType = dec.ReturnType.ToString()
            Parameters = convertParams dec.Parameters
        }

    let private serialize (dec: ExternalDeclaration) =
        JsonConvert.SerializeObject(dec)
            
    let export (path: string) =
        let json =
            getDeclaration()
            |> exportDeclaration
            |> serialize
        File.WriteAllText(Path.Combine(path, "ipc.declaration.json"), json)

    type DeclarationManager (dec: ExternalDeclaration) =
        let exDec = dec
        let mutable inDec: InternalDeclaration = Seq.empty

        let validateDeclaration () = 
            inDec
            |> Seq.toList
            |> List.map (fun inFunc ->
                exDec
                |> Seq.exists (fun exFunc -> equalDecsElem inFunc exFunc)
                |> function
                | true -> ()
                | false -> failwithf "The method with name '%s' was exported as an IPCMethod, but a matching signature cannot be found in the specified declaration. This suggests that the declaration file was not generated from the current build. Please re-generate the declaration file." inFunc.Name)
            |> ignore

            exDec
            |> Seq.toList
            |> List.map (fun exFunc ->
                inDec
                |> Seq.exists (fun inFunc -> equalDecsElem inFunc exFunc)
                |> function
                | true -> ()
                | false -> failwithf "The method with name '%s' was found in the specified declaration, but a matching signature cannot be found in this project. This suggests that the declaration file was not generated from the current build. Please re-generate the declaration file." exFunc.Name)
            |> ignore
        
        do
            inDec <- getDeclaration()
            validateDeclaration ()

        member this.Execute (methodName: string) (paramArr: obj []): obj =
            inDec
            |> Seq.tryFind (fun method -> method.Name = methodName)
            |> function
            | None -> failwithf "Method '%s' execution was attempted, but this method does not exist." methodName
            | Some method -> method.Method.Invoke(null, paramArr)

        member this.ParseParams (methodName: string) (parameters: JArray) : Result<obj[],string> =
            try
                inDec 
                |> Seq.tryFind (fun m -> m.Name = methodName)
                |> function
                | None -> Error <| sprintf "Could not find specified method: %s" methodName
                | Some method ->
                    if Seq.length method.Parameters <> parameters.Count
                    then Error <| sprintf "Wrong number of parameters provided. This method expects %i, but got %i" (Seq.length method.Parameters) parameters.Count
                    else 
                        (method.Parameters, parameters)
                        ||> Seq.zip
                        |> Seq.map (fun (paramInfo, jToken) ->
                            jToken.ToObject paramInfo.Type
                            |> box)
                        |> Seq.toArray
                        |> Ok
            with
            | e -> Error <| sprintf "Unable to parse the parameter to the correct types: %s" e.Message  // TODO: maybe do more analysis
