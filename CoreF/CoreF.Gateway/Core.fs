namespace CoreF.Gateway

open CoreF.Http.Auditing
open CoreF.Http.Caching
open CoreF.Http.Middleware
open CoreF.Http.Tracing
open Microsoft.AspNetCore.Builder
open System.Runtime.CompilerServices

module CoreF =
    let useApiGateway (app: IApplicationBuilder) =
        app.UseMiddleware<TracingMiddleware>()
           .UseMiddleware<AuditingMiddleware>()
           .UseMiddleware<CachingMiddleware>()
           .UseMiddleware<HttpMiddleware>()


[<Extension>]
type AspNetExtensions =
    [<Extension>]
    static member UseCoreF (app: IApplicationBuilder) = 
        CoreF.useApiGateway app