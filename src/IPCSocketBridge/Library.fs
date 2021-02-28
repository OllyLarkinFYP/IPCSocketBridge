namespace IPCSocketBridge

open System
open System.Reflection

module CollectMethods =
    let getMethods () =
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
        |> Declaration.generateDeclaration
