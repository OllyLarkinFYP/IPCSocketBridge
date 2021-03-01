namespace IPCSocketBridge

open System
open System.Reflection
open System.IO
open Newtonsoft.Json
open Utils

module Declaration =
    type InternalDeclarationElement = {
        Name: string
        ReturnType: string
        Parameters: {| Name: string; Type: string |} seq
        Method: MethodInfo
    }
    type InternalDeclaration = InternalDeclarationElement seq

    type ExternalDeclarationElement = {
        Name: string
        ReturnType: string
        Parameters: {| Name: string; Type: string |} seq
    }
    type ExternalDeclaration = ExternalDeclarationElement seq

    let equalDecsElem (inDecElem: InternalDeclarationElement) (exDecElem: ExternalDeclarationElement) =
        inDecElem.Name = exDecElem.Name
        && seqCompare inDecElem.Parameters exDecElem.Parameters
        && inDecElem.ReturnType = exDecElem.ReturnType

    let private generateDeclaration (methods: (string * MethodInfo) seq) : InternalDeclaration =
        methods
        |> Seq.map (fun (name, method) -> 
            let parameters = 
                method.GetParameters()
                |> Seq.map (fun param -> {| Name = param.Name; Type = param.ParameterType.ToString()|})
            {
                Name = name
                ReturnType = method.ReturnType.ToString()
                Parameters = parameters
                Method = method
            })

    let private validateIPCMethod () =
        let mutable names = List.empty
        fun (name, (method: MethodInfo)) ->
            if method.IsGenericMethod || method.ContainsGenericParameters
            then failwithf "Generics are currently not supported for IPC methods: %s, %A" name method
            
            if List.contains name names
            then failwithf "IPC method names must be unique. '%s' was found multiple times. Use the format [<IPCMethod(\"example\")>] to use a different name." name
            else names <- name::names

            name, method

    let private getDeclaration () =
        let validator = validateIPCMethod()
        AppDomain.CurrentDomain.GetAssemblies()
        |> Seq.collect (fun assembly -> assembly.GetTypes())
        |> Seq.collect (fun typ -> typ.GetMethods())
        |> Seq.choose (fun method ->
            method.GetCustomAttribute<IPCMethodAttribute>()
            |> nullableToOption
            |> Option.map (fun attr -> attr.Name, method))
        |> Seq.map validator
        |> generateDeclaration

    let private exportDeclaration : InternalDeclaration -> ExternalDeclaration =
        Seq.map <| fun dec -> {
            Name = dec.Name
            ReturnType = dec.ReturnType
            Parameters = dec.Parameters
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
 