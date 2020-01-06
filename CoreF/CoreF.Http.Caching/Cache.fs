namespace CoreF.Http.Caching

open CoreF.DependencyInjection
open CoreF.Serialization
open Microsoft.Extensions.Caching.Distributed

module Cache =
    let tryFind<'value> (key: CacheKey) =
        injectedAsync {
            let! cache = inject<IDistributedCache>() |> Injected.mapError DependencyInjectionError
            let! serializer = inject<ISerializer>() |> Injected.mapError DependencyInjectionError
            let! cacheData = key |> CacheKey.value |> cache.GetAsync |> Async.AwaitTask
            match cacheData |> Option.ofObj with
            | Some [||] -> 
                return None
            | Some bytes ->
                let! value = bytes |> serializer.Deserialize<'value> |> Result.mapError SerializationError
                return Some value
            | None ->
                return None
        }

    let add<'value> (key: CacheKey) (value: 'value) =
        injectedAsync {
            let! cache = inject<IDistributedCache>() |> Injected.mapError DependencyInjectionError
            let! serializer = inject<ISerializer>() |> Injected.mapError DependencyInjectionError
            let! data = serializer.Serialize(value) |> Result.mapError SerializationError
            let cacheKey = key |> CacheKey.value
            return! cache.SetAsync(cacheKey, data) |> Async.AwaitTask
        }

    let getOrAdd<'value> (key: CacheKey) (f: unit -> Async<'value>) =
        injectedAsync {
            match! key |> tryFind<'value> with
            | Some value ->
                return value
            | None ->
                let! value = f ()
                do! value |> add<'value> key
                return value
        }