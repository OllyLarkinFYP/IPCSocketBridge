namespace IPCSocketBridge

open System
open System.Net
open System.Net.Sockets
open Declaration
open Newtonsoft.Json
open Newtonsoft.Json.Linq

module IPC =

    type IncomingMessage = {
        [<JsonProperty(Required = Required.Always)>]
        ID: int

        [<JsonProperty(Required = Required.Always)>]
        Name: string

        [<JsonProperty(Required = Required.Always)>]
        Parameters: JArray 
    }

    type IPCManager (port: int, dec: ExternalDeclaration) =
        let decManager = DeclarationManager(dec)

        let parseAndExecute (dec: ExternalDeclaration) (message: byte[]) =
            let messageStr = System.Text.Encoding.ASCII.GetString message
            printfn "[IPC] Message to parse:\n%A" messageStr
            let recObj = JsonConvert.DeserializeObject<IncomingMessage>(messageStr)
            printfn "[IPC] Parsed object:\n%A" recObj

            recObj.Parameters
            |> decManager.ParseParams recObj.Name
            |> function
            | Error err -> failwithf "parsing failed: %s" err
            | Ok x -> 
                x
                |> decManager.Execute recObj.Name
                |> printfn "[IPC] Function call return: %A"

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
            receiveExact socket buff 4      // TODO: handle exceptions
            BitConverter.ToInt32(buff,0)

        let receive (socket: Socket) (size: int) : byte[] =
            let mutable buff: byte[] = Array.zeroCreate size
            receiveExact socket buff size   // TODO: handle exceptions
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
