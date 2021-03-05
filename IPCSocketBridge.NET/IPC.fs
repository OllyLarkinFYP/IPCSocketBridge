namespace IPCSocketBridge

open System
open System.IO
open System.Net
open System.Net.Sockets
open Declaration
open Newtonsoft.Json
open Newtonsoft.Json.Linq
open Utils

module IPC =

    type IncomingMessage = {
        [<JsonProperty(Required = Required.Always)>]
        ID: int

        [<JsonProperty(Required = Required.Always)>]
        Name: string

        [<JsonProperty(Required = Required.Always)>]
        Parameters: JArray 
    }

    type IPCManager (port: int, decPath: string) =
        let dec = 
            (fun () ->
                File.ReadAllText(decPath)
                |> JsonConvert.DeserializeObject<ExternalDeclaration>)
            |> tryToResult
            |> function
            | Ok d -> d
            | Error e -> 
                printfn "[IPC] Unable to read/parse specified declaration file. Please make sure the path is correct and that the declaration file was created from the same build."
                raise e

        let decManager = DeclarationManager(dec)

        let sendMessageSize (socket: Socket) (size: int) =
            let message: byte[] = BitConverter.GetBytes(size)
            socket.Send(message, 4, SocketFlags.None) 
            |> ignore

        let sendMessage (socket: Socket) (message: string) =
            let messageBytes: byte[] = Text.Encoding.ASCII.GetBytes(message)
            socket.Send(messageBytes, messageBytes.Length, SocketFlags.None) 
            |> ignore

        let sendReply (socket: Socket) (id: int) (retObj: obj) =
            let message = JsonConvert.SerializeObject retObj
            sendMessageSize socket message.Length
            sendMessage socket message
            printfn "[IPC] Method return sent"

        /// If successful, returns result containing the ID of the function call and the result
        let parseAndExecute (dec: ExternalDeclaration) (message: byte[]) =
            let messageStr = System.Text.Encoding.ASCII.GetString message
            printfn "[IPC] Message to parse:\n%A" messageStr
            let receivedMethod = JsonConvert.DeserializeObject<IncomingMessage>(messageStr)
            printfn "[IPC] Parsed object:\n%A" receivedMethod

            receivedMethod.Parameters
            |> decManager.ParseParams receivedMethod.Name
            |> function
            | Error err -> Error <| sprintf "[IPC] Parsing failed: %s" err
            | Ok x -> 
                x
                |> decManager.Execute receivedMethod.Name
                |> teef "[IPC] Function call return: %A"
                |> tuple receivedMethod.ID
                |> Ok

        let rec receiveExactRec (socket: Socket) buff offset size =
            let numReceieved = socket.Receive(buff, offset, size, SocketFlags.None)
            let newOffset = offset + numReceieved
            let newSize = size - numReceieved

            if newSize <= 0
            then ()
            else receiveExactRec socket buff newOffset newSize

        let receiveExact socket buff size =
            receiveExactRec socket buff 0 size

        let getMessageSize (socket: Socket) : int =
            let mutable buff: byte[] = Array.zeroCreate 4
            receiveExact socket buff 4      // TODO: handle exceptions (currently in openCommunication)
            BitConverter.ToInt32(buff,0)

        let receive (socket: Socket) (size: int) : byte[] =
            let mutable buff: byte[] = Array.zeroCreate size
            receiveExact socket buff size   // TODO: handle exceptions (currently in openCommunication)
            buff

        let openCommunication (dec: ExternalDeclaration) (socket: Socket) =
            printfn "[IPC] Connection found"
            printfn "[IPC] Waiting for incoming message..."
            while socket.Connected do
                try
                    // Message size should correspond to the number of bytes in the following message
                    getMessageSize socket
                    |> receive socket
                    |> parseAndExecute dec
                    |> function
                    | Ok (id, retObj) -> sendReply socket id retObj
                    | Error err -> printfn "[IPC] Unable to parse and execute specified method: %s" err
                with
                | :? SocketException as ex -> printfn "[IPC] Socket Exception: %s" ex.Message
            printfn "[IPC] Disconnected"

        member this.Start () =
            let local = IPAddress.Parse "127.0.0.1"
            let socketListener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            let endPoint = IPEndPoint(local, port)
            socketListener.Bind endPoint
            socketListener.Listen()

            printfn "[IPC] Listening on port %i" port

            while true do
                printfn "[IPC] Waiting for incoming connection..."
                socketListener.Accept() |> openCommunication dec
