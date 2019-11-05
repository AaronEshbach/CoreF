namespace CoreF.Serialization

open CoreF.Common
open System

module Serialization =
    let private useSerializer f =
        inject {
            let! serializer = resolve<ISerializer>()
            return f serializer
        }

    let toBytes (object: 't) =
        useSerializer <| fun serializer -> serializer.Serialize(object)

    let toString (object: 't) =
        useSerializer <| fun serializer -> serializer.SerializeToString(object)

    let toStream stream (object: 't) =
        useSerializer <| fun serializer -> serializer.SerializeToStream stream object

    let toContent (object: 't) =
        useSerializer <| fun serializer -> serializer.SerializeToContent(object)

    let parseBytes<'t> bytes =
        useSerializer <| fun serializer -> serializer.Deserialize<'t>(bytes)

    let parseString<'t> str =
        useSerializer <| fun serializer -> serializer.DeserializeString<'t>(str)

    let parseStream<'t> stream =
        useSerializer <| fun serializer -> serializer.DeserializeStream<'t>(stream)

    let parseContent<'t> content =
        useSerializer <| fun serializer -> serializer.DeserializeContent<'t>(content)

    let parseBytesAs (objectType: Type) bytes =
        useSerializer <| fun serializer -> serializer.DeserializeAsType objectType bytes

    let parseStringAs (objectType: Type) str =
        useSerializer <| fun serializer -> serializer.DeserializeStringAsType objectType str

    let parseStreamAs (objectType: Type) stream =
        useSerializer <| fun serializer -> serializer.DeserializeStreamAsType objectType stream

    let parseContentAs (objectType: Type) content =
        useSerializer <| fun serializer -> serializer.DeserializeContentAsType objectType content