namespace CoreF.Serialization

open CoreF.Common
open System.IO
open System.Net.Http

type JsonSerializer () =
    let serializer = Newtonsoft.Json.JsonSerializer()

    let serializeToStream (stream: Stream) value =
        result {
            use writer = new StreamWriter(stream)            
            try
                serializer.Serialize(writer, value)
            with ex ->
                let valueType = value.GetType()
                return! Error <| ErrorSerializingType (valueType, ex)
        }

    let serialize value =
        result {
            use stream = new MemoryStream()            
            do! serializeToStream stream value
            return stream.ToArray()
        }

    let serializeToString value =
        value |> serialize |> Result.map Utf8.toString

    interface ISerializer with
        member __.Serialize<'t> (value: 't) =
            serialize value
        member __.SerializeToString<'t> (value: 't) =
            serializeToString value
        member __.SerializeToStream<'t> stream (value: 't) =
            value |> serializeToStream stream
        member __.SerializeToContent<'t> (value: 't) =
            value |> serializeToString |> Result.map (fun s -> new StringContent(s, Utf8.encoding) :> HttpContent)

        member __.Deserialize<'t> bytes =
            result {
                use stream = new MemoryStream(bytes)
                use reader = new StreamReader(stream)
                use json = new Newtonsoft.Json.JsonTextReader(reader)
                try                    
                    return serializer.Deserialize<'t>(json)
                with ex ->
                    let objectType = typeof<'t>
                    return! Error <| ErrorDeserializingBytes (bytes, objectType, ex)
            }

        member __.DeserializeString<'t> str =
            result {
                use reader = new StringReader(str)
                use json = new Newtonsoft.Json.JsonTextReader(reader)
                try                    
                    return serializer.Deserialize<'t>(json)
                with ex ->
                    let objectType = typeof<'t>
                    return! Error <| ErrorDeserializingString (str, objectType, ex)
            }

        member __.DeserializeStream<'t> stream =
            result {
                use reader = new StreamReader(stream)
                use json = new Newtonsoft.Json.JsonTextReader(reader)
                try                    
                    return serializer.Deserialize<'t>(json)
                with ex ->
                    let objectType = typeof<'t>
                    return! Error <| ErrorDeserializingStream (objectType, ex)
            }

        member __.DeserializeContent<'t> content =
            asyncResult {
                use! stream = content.ReadAsStreamAsync().ToAsyncResult(UnexpectedSerializationError)
                use reader = new StreamReader(stream)
                use json = new Newtonsoft.Json.JsonTextReader(reader)
                try                    
                    return serializer.Deserialize<'t>(json)
                with ex ->
                    let objectType = typeof<'t>
                    return! Error <| ErrorDeserializingStream (objectType, ex)
            }

        member __.DeserializeAsType objectType bytes =
            result {
                use stream = new MemoryStream(bytes)
                use reader = new StreamReader(stream)
                use json = new Newtonsoft.Json.JsonTextReader(reader)
                try                    
                    return serializer.Deserialize(json, objectType)
                with ex ->
                    return! Error <| ErrorDeserializingBytes (bytes, objectType, ex)
            }

        member __.DeserializeStringAsType objectType str =
            result {
                use reader = new StringReader(str)
                use json = new Newtonsoft.Json.JsonTextReader(reader)
                try                    
                    return serializer.Deserialize(json, objectType)
                with ex ->
                    return! Error <| ErrorDeserializingString (str, objectType, ex)
            }

        member __.DeserializeStreamAsType objectType stream =
            result {
                use reader = new StreamReader(stream)
                use json = new Newtonsoft.Json.JsonTextReader(reader)
                try                    
                    return serializer.Deserialize(json, objectType)
                with ex ->
                    return! Error <| ErrorDeserializingStream (objectType, ex)
            }

        member __.DeserializeContentAsType objectType content =
            asyncResult {
                use! stream = content.ReadAsStreamAsync().ToAsyncResult(UnexpectedSerializationError)
                use reader = new StreamReader(stream)
                use json = new Newtonsoft.Json.JsonTextReader(reader)
                try                    
                    return serializer.Deserialize(json, objectType)
                with ex ->
                    return! Error <| ErrorDeserializingStream (objectType, ex)
            }