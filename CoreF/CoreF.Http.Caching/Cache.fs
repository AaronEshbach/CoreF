namespace CoreF.Http.Caching

open CoreF.DependencyInjection
open CoreF.Serialization
open Microsoft.Extensions.Caching.Distributed

module Cache =
    let tryFind<'key, 'value when 'key :> IKey> (key: 'key) =
        injectedAsync {
            let! cache = inject<IDistributedCache>() |> Injected.mapError DependencyInjectionError
            let! serializer = inject<ISerializer>() |> Injected.mapError DependencyInjectionError
            let! cacheData = cache.GetAsync(key.ToCacheKey()) |> Async.AwaitTask
            match cacheData |> Option.ofObj with
            | Some [||] -> 
                return None
            | Some bytes ->
                let! value = bytes |> serializer.Deserialize<'value> |> Result.mapError SerializationError
                return Some value
            | None ->
                return None
        }