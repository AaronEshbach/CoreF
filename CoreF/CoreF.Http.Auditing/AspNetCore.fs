namespace CoreF.Http.Auditing

open Microsoft.AspNetCore.Builder
open System.Runtime.CompilerServices
 
module CoreF =
    let useAuditingMiddleware (app: IApplicationBuilder) =
        app.UseMiddleware<AuditingMiddleware>()

[<Extension>]
type AspNetExtensions =
    [<Extension>]
    static member UseCoreFAuditing (app: IApplicationBuilder) = 
        CoreF.useAuditingMiddleware app