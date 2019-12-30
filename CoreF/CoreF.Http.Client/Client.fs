namespace CoreF.Http.Client

open CoreF.Common
open CoreF.DependencyInjection
open CoreF.Serialization
open CoreF.Validation

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

    let rec sendRequest<'dto, 'error> (f: HttpClient -> Uri -> Async<HttpResponseMessage>) (uri: Uri) (client: HttpClient) : HttpClientCall<'dto, 'error> =
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

    let get<'dto, 'error> uri client : HttpClientCall<'dto, 'error> =
        client |> sendRequest<'dto, 'error> (fun client url -> client.GetAsync(url) |> Async.AwaitTask) uri

    let post<'request, 'response, 'error> uri (request: 'request) client : HttpClientCall<'response, 'error> =
        injectedAsync {
            let! requestContent = request |> Serializer.toContent |> Injected.mapError DeserializationError
            let (HttpClientCall result) = client |> sendRequest<'response, 'error> (fun client url -> client.PostAsync(url, requestContent) |> Async.AwaitTask) uri
            return! result
        } |> HttpClientCall

    let put<'request, 'error> uri (request: 'request) client : HttpClientCall<unit, 'error> =
        injectedAsync {
            let! requestContent = request |> Serializer.toContent |> Injected.mapError DeserializationError
            let (HttpClientCall result) = client |> sendRequest<unit, 'error> (fun client url -> client.PutAsync(url, requestContent) |> Async.AwaitTask) uri
            return! result
        } |> HttpClientCall

    let patch<'request, 'error> uri (request: 'request) client : HttpClientCall<unit, 'error> =
        injectedAsync {
            let! requestContent = request |> Serializer.toContent |> Injected.mapError DeserializationError
            let (HttpClientCall result) = 
                client |> sendRequest<unit, 'error> (fun client url -> 
                    use request = new HttpRequestMessage(HttpMethod("PATCH"), url)
                    request.Content <- requestContent
                    client.SendAsync(request) |> Async.AwaitTask) uri
            return! result
        } |> HttpClientCall

    let delete<'error> uri client : HttpClientCall<unit, 'error> =
        client |> sendRequest<unit, 'error> (fun client url -> client.DeleteAsync(url) |> Async.AwaitTask) uri

    let map<'dto, 'model, 'validationError, 'clientError> (mapError: 'validationError list -> 'clientError) (create: 'dto -> ValidatedResult<'model, 'validationError>) ((HttpClientCall call): HttpClientCall<'dto, 'clientError>) =
        injectedAsync {
            let! response = call
            match response with
            | HttpOk dto ->
                let! model = dto |> create |> ValidatedResult.toResult |> Result.mapError mapError |> Result.mapError ClientProcessingError
                return HttpOk model
            | HttpRedirect location ->
                return HttpRedirect location
            | HttpClientError error ->
                return HttpClientError error
            | HttpServerError error ->
                return HttpServerError error
        } |> HttpClientCall

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