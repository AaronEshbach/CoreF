namespace CoreF.Http.Tracing

open Microsoft.AspNetCore.Builder
open System.Runtime.CompilerServices
 
module CoreF =
    let useTracingMiddleware (app: IApplicationBuilder) =
        app.UseMiddleware<TracingMiddleware>()

[<Extension>]
type AspNetExtensions =
    [<Extension>]
    static member UseCoreFTracing (app: IApplicationBuilder) = 
        CoreF.useTracingMiddleware app