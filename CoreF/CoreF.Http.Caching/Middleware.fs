namespace CoreF.Http.Caching

open CoreF.Common
open CoreF.DependencyInjection
open Microsoft.AspNetCore.Http

type CachingMiddleware (next: RequestDelegate, cacheManager: ICacheManager) =

    member __.InvokeAsync (context: HttpContext) =
        asyncResult {            
            let! response = 
                injectedAsync {
                    let! key = context.Request |> CacheKey.generate
                    return! 
                        Cache.getOrAdd key (fun _ -> async {
                            do! next.Invoke(context) |> Async.AwaitTask
                            return context.Response
                        }) 
                } |> DependencyInjection.resolveAsync context.RequestServices
                
            context.Response.StatusCode <- response.StatusCode
            context.Response.Body <- response.Body
            
            for header in response.Headers do
                context.Response.Headers.[header.Key] <- header.Value

        } |> AsyncResult.toTask :> System.Threading.Tasks.Task
        
        
