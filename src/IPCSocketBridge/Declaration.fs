namespace IPCSocketBridge

open System.Reflection
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

    let generateDeclaration (methods: (string * MethodInfo) seq) : InternalDeclaration =
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

    let exportDeclaration : InternalDeclaration -> ExternalDeclaration =
        Seq.map <| fun dec -> {
            Name = dec.Name
            ReturnType = dec.ReturnType
            Parameters = dec.Parameters
        }

    let serializeDeclaration (dec: ExternalDeclaration) =
        JsonConvert.SerializeObject(dec)