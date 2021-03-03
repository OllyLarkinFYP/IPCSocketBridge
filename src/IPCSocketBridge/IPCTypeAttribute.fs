namespace IPCSocketBridge

open System
open System.Runtime.CompilerServices
open System.Runtime.InteropServices

[<AttributeUsage(AttributeTargets.Class ||| AttributeTargets.Struct)>]
type IPCTypeAttribute() = 
    inherit Attribute()

// TODO: implement the IPCType stuff
