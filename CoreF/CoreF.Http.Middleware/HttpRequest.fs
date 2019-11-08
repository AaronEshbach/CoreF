namespace CoreF.Http.Middleware

open CoreF.Common
open CoreF.Http
open Microsoft.Extensions.Primitives
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Extensions
open System
open System.Net

type AspNetHttpRequest (context: HttpContext, body: byte []) =
    let request = context.Request
    let url = UriHelper.GetEncodedUrl request
    let timestamp = DateTime.UtcNow

    let verb = 
        match request.Method with
        | Like "GET" -> HttpMethod.Get
        | Like "PUT" -> HttpMethod.Put
        | Like "POST" -> HttpMethod.Post
        | Like "PATCH" -> HttpMethod.Patch
        | Like "DELETE" -> HttpMethod.Delete
        | Like "HEAD" -> HttpMethod.Head
        | Like "OPTIONS" -> HttpMethod.Options
        | other -> HttpMethod.Custom other

    let requestId =
        match RequestId.tryParse context.TraceIdentifier with
        | Some id -> id
        | None -> RequestId.create()

    let headers = 
        let getValue (strings: StringValues) = strings |> String.join ","
        [for header in request.Headers -> CaseInsensitiveString.create header.Key, getValue header.Value] |> Map.ofList

    let client =
        {new IHttpClientMetadata with
            member __.Host = 
                try Dns.GetHostEntry(context.Connection.RemoteIpAddress).HostName
                with _ -> context.Connection.RemoteIpAddress.ToString()
            member __.User = 
                context.User.Identity.Name
        }

    interface IHttpRequest with
        member __.Url = Uri url
        member __.Method = verb
        member __.RequestId = requestId
        member __.Headers = headers
        member __.Timestamp = timestamp
        member __.ServerHost = Environment.MachineName
        member __.Client = client
        member __.Body = body
