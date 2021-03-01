namespace IPCSocketBridge

open System
open System.Reflection
open System.IO
open Newtonsoft.Json

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

    let private getDeclaration () =
        AppDomain.CurrentDomain.GetAssemblies()
        |> Seq.collect (fun assembly -> assembly.GetTypes())
        |> Seq.collect (fun typ -> typ.GetMethods())
        |> Seq.choose (fun method ->
            method.GetCustomAttribute<IPCMethodAttribute>()
            |> Utils.nullableToOption
            |> Option.map (fun attr -> attr.Name, method))
        |> Seq.map (fun (name, method) -> 
            if method.IsGenericMethod || method.ContainsGenericParameters
            then failwithf "Cannot export generics for IPC: %s, %A" name method     // TODO: make error message better
            else name, method
            )
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

