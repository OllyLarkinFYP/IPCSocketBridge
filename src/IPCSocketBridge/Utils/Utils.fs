namespace IPCSocketBridge

open System

module Utils =
    let nullableToOption a =
        match box a with
        | null -> None
        | _ -> Some a

    let tee a =
        printfn "%A" a
        a

    let teef str a =
        printfn str a
        a

    let seqCompare a b =
        Seq.fold (&&) true (Seq.zip a b |> Seq.map (fun (aa,bb) -> aa=bb))

    let tryToResult f =
        try
            Ok <| f ()
        with
        | e -> Error e

    let tuple a b = a,b

    let rtuple a b = b,a

    let notImplemented (): 'a = raise <| NotImplementedException()