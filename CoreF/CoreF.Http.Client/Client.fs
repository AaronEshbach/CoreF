namespace CoreF.Http.Client

open CoreF.Common
open CoreF.DependencyInjection
open CoreF.Serialization

open System
open System.Net
open System.Net.Http

module HttpClient =
    let internal isRedirect (status: HttpStatusCode) =
        let statusCode = status |> int
        statusCode = 301 || (statusCode > 306 && statusCode < 310)

    let Default = id

    let create fSetup handler =
        let client = 
            match handler with
            | Some handler ->
                new HttpClient(handler)
            | None ->
                new HttpClient()

        fSetup client
        client

    let rec sendRequest<'dto> (f: HttpClient -> Uri -> Async<HttpResponseMessage>) (uri: Uri) (client: HttpClient) : HttpClientCall<'dto> =
        injectedAsync {
            let! response = f client uri
            match response with
            | success when response.IsSuccessStatusCode ->
                if typeof<'dto> = typeof<unit> then
                    let dto = () |> unbox<'dto>
                    return HttpOk dto
                else
                    let! dto = success.Content |> Serializer.parseContent<'dto> |> InjectedAsync.mapError DeserializationError
                    return HttpOk dto
            | error ->
                match int error.StatusCode with
                | 302 | 303 | 307 | 308 ->
                    let location = response.Headers.Location
                    return HttpRedirect location
                | 400 ->
                    return HttpClientError (BadRequest error)
                | 401 | 403 ->
                    return HttpClientError Unauthorized
                | 404 ->
                    return HttpClientError NotFound
                | 406 ->
                    return HttpClientError NotAcceptable
                | 422 ->
                    return HttpClientError (UnprocessableEntity error)
                | serverError when serverError >= 500 ->
                    return HttpServerError error
                | other ->
                    return HttpClientError (OtherClientError (other, error))
        } |> HttpClientCall

    let get<'dto> uri client : HttpClientCall<'dto> =
        client |> sendRequest<'dto> (fun client url -> client.GetAsync(url) |> Async.AwaitTask) uri

    let post<'request, 'response> uri (request: 'request) client : HttpClientCall<'response> =
        injectedAsync {
            let! requestContent = request |> Serializer.toContent |> Injected.mapError SerializationError
            let (HttpClientCall result) = client |> sendRequest<'response> (fun client url -> client.PostAsync(url, requestContent) |> Async.AwaitTask) uri
            return! result
        } |> HttpClientCall

    let put<'request> uri (request: 'request) client : HttpClientCall<unit> =
        injectedAsync {
            let! requestContent = request |> Serializer.toContent |> Injected.mapError SerializationError
            let (HttpClientCall result) = client |> sendRequest<unit> (fun client url -> client.PutAsync(url, requestContent) |> Async.AwaitTask) uri
            return! result
        } |> HttpClientCall

    let patch<'request> uri (request: 'request) client : HttpClientCall<unit> =
        injectedAsync {
            let! requestContent = request |> Serializer.toContent |> Injected.mapError SerializationError
            let (HttpClientCall result) = 
                client |> sendRequest<unit> (fun client url -> 
                    use request = new HttpRequestMessage(HttpMethod("PATCH"), url)
                    request.Content <- requestContent
                    client.SendAsync(request) |> Async.AwaitTask) uri
            return! result
        } |> HttpClientCall

    let delete uri client : HttpClientCall<unit> =
        client |> sendRequest<unit> (fun client url -> client.DeleteAsync(url) |> Async.AwaitTask) uri


module Url =
    let parse (url: string) =
        match url |> Url.tryParse with
        | Some uri -> Ok uri
        | None -> Error InvalidRequestUri

    let makeRelative (fragment: string) baseUrl =
        match Uri.TryCreate(baseUrl, fragment) with
        | (true, uri) -> Ok uri
        | _ -> Error InvalidRequestUri

    let makeRelativef formatFragment =
        Printf.kprintf makeRelative formatFragment