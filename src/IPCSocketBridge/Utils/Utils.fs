namespace IPCSocketBridge

module Utils =
    let nullableToOption a =
        match box a with
        | null -> None
        | _ -> Some a

    let tee a =
        printfn "%A" a
        a