namespace CoreF.Http.Caching

open CoreF.Common
open CoreF.DependencyInjection
open CoreF.Serialization

type HttpCacheError =
| DependencyInjectionError of DependencyInjectionError
| SerializationError of SerializationError
| CacheKeyCannotBeEmpty
| KeyGeneratorNotFound of System.Type
| UnexpectedCacheError of exn

[<Struct>]
type CacheKey = private CacheKey of string

type IKeyGenerator<'model> =
    abstract member GetKey : 'model -> CacheKey

type ICacheManager = interface end

module CacheKey =   
    let create (key: string) =
        if key |> isNullOrEmpty then
            Error CacheKeyCannotBeEmpty
        else
            Ok <| CacheKey key

    let generate<'t> (value: 't) =
        injected {
            let! generator = inject<IKeyGenerator<'t>>()
            return generator.GetKey value
        } |> Injected.mapError (fun _ -> KeyGeneratorNotFound typeof<'t>)

    let value (CacheKey key) = key

