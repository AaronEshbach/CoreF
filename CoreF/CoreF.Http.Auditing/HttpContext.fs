namespace CoreF.Http.Auditing

open CoreF.Common
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Http.Extensions
open Microsoft.Extensions.Primitives
open System
open System.IO
open System.Net

module HttpContext =
    let private getHeaders (headers: IHeaderDictionary) = 
        let getValue (strings: StringValues) = strings |> String.join ","
        [for header in headers -> header.Key, getValue header.Value] |> Map.ofList

    let toRequest (context: HttpContext) =
        async {
            let request = context.Request
            let url = UriHelper.GetEncodedUrl request
            let timestamp = DateTime.UtcNow
            let requestId = context.TraceIdentifier
            let headers = request.Headers |> getHeaders                

            let client =
                {
                    Host = 
                        try Dns.GetHostEntry(context.Connection.RemoteIpAddress).HostName
                        with _ -> context.Connection.RemoteIpAddress.ToString()
                    User =
                        context.User.Identity.Name
                }

            let! body = 
                async {
                    use copy = new MemoryStream()
                    do! request.Body.CopyToAsync(copy) |> Async.AwaitTask
                    let body = copy.ToArray()
                    copy.Position <- 0L
                    request.Body <- copy
                    return body
                }

            return 
                {
                    Url = Uri url
                    Method = request.Method
                    RequestId = requestId
                    Headers = headers
                    Timestamp = timestamp
                    Server = Environment.MachineName
                    Client = client
                    Body = body
                }
        }

    let toResponse (request: CoreF.Http.Auditing.HttpRequest) (context: HttpContext) =
        async {
            let response = context.Response
            let timestamp = DateTime.UtcNow
            let headers = response.Headers |> getHeaders

            let! body = 
                async {
                    use copy = new MemoryStream()
                    do! response.Body.CopyToAsync(copy) |> Async.AwaitTask
                    let body = copy.ToArray()
                    copy.Position <- 0L
                    response.Body <- copy
                    return body
                }

            return
                {
                    RequestId = request.RequestId
                    Headers = headers
                    StatusCode = response.StatusCode
                    Body = body
                    Timestamp = timestamp
                    ElapsedTime = timestamp - request.Timestamp
                }
        }