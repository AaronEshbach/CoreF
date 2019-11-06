﻿namespace CoreF.Serialization

open CoreF.Common
open System
open System.IO
open System.Net.Http

type SerializationError =
| ErrorSerializingType of Type * exn
| ErrorDeserializingString of string * Type * exn
| ErrorDeserializingBytes of byte [] * Type * exn
| ErrorDeserializingStream of Type * exn
| ErrorDeserializingContent of HttpContent * Type * exn
| UnexpectedSerializationError of exn

type ISerializer =
    abstract member Deserialize<'t> : byte [] -> Result<'t, SerializationError>
    abstract member DeserializeContent<'t> : HttpContent -> AsyncResult<'t, SerializationError>
    abstract member DeserializeString<'t> : string -> Result<'t, SerializationError>
    abstract member DeserializeStream<'t> : Stream -> Result<'t, SerializationError>

    abstract member DeserializeAsType : Type -> byte [] -> Result<obj, SerializationError>
    abstract member DeserializeContentAsType : Type -> HttpContent -> AsyncResult<obj, SerializationError>
    abstract member DeserializeStringAsType : Type -> string -> Result<obj, SerializationError>
    abstract member DeserializeStreamAsType : Type -> Stream -> Result<obj, SerializationError>

    abstract member Serialize<'t> : 't -> Result<byte [], SerializationError>
    abstract member SerializeToContent<'t> : 't -> Result<HttpContent, SerializationError>
    abstract member SerializeToString<'t> : 't -> Result<string, SerializationError>
    abstract member SerializeToStream<'t> : Stream -> 't -> Result<unit, SerializationError>