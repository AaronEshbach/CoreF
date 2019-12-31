namespace CoreF.Http.Middleware

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Hosting
open System.Runtime.CompilerServices
 
module CoreF =
    let getConfig (env: IHostEnvironment) = 
        ConfigurationBuilder()
            .SetBasePath(env.ContentRootPath)
            .AddJsonFile("appsettings.json", optional = true, reloadOnChange = true)
            .AddJsonFile(sprintf "appsettings.%s.json" env.EnvironmentName, optional = true)
            .AddEnvironmentVariables()
            .Build()

    let useHttpMiddleware (app: IApplicationBuilder) =
        app.UseMiddleware<HttpMiddleware>()

[<Extension>]
type AspNetExtensions =
    [<Extension>]
    static member UseCoreFHttp (app: IApplicationBuilder) = 
        CoreF.useHttpMiddleware app