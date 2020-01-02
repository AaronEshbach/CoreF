namespace CoreF.Http.Caching

open CoreF.DependencyInjection
open CoreF.Serialization

type HttpCacheError =
| DependencyInjectionError of DependencyInjectionError
| SerializationError of SerializationError
| UnexpectedCacheError of exn

type IKey =
    abstract member ToCacheKey: unit -> string

