namespace CoreF.Mapping

open AutoMapper
open System

[<StructuredFormatDisplay("{AsString}")>]
type MappingError =
| AutoMapperInitializationFailed of exn
| NoTypeMappingFound of (TypeMap -> bool)
| ErrorMappingTypes of (Type * Type * exn)

    override this.ToString () = 
        match this with
        | AutoMapperInitializationFailed ex -> sprintf "AutoMapper initialization failed: %s" ex.Message
        | NoTypeMappingFound f -> sprintf "No type mapping found: %A" f
        | ErrorMappingTypes (t1,t2,ex) -> sprintf "Error mapping types %s -> %s: %s" t1.FullName t2.FullName ex.Message

    member this.AsString = this.ToString()