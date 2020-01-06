namespace CoreF.Http.Caching

open Microsoft.AspNetCore.Builder

open System.Runtime.CompilerServices
 
module CoreF =
    let useCaching (app: IApplicationBuilder) =
        app.UseMiddleware<CachingMiddleware>()

[<Extension>]
type AspNetExtensions =
    [<Extension>]
    static member UseCoreFCaching (app: IApplicationBuilder) = 
        CoreF.useCaching app