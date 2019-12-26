namespace CoreF.Http.Client

open CoreF.Common
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

    let rec sendRequest<'dto> (f: HttpClient -> Uri -> Async<HttpResponseMessage>) (uri: Uri) (client: HttpClient) : AsyncHttpClientResponse<'dto> =
        injectedAsync {
            let! response = f client uri
            match response with
            | success when response.IsSuccessStatusCode ->
                if typeof<'dto> = typeof<unit> then
                    let dto = () |> unbox<'dto>
                    return HttpOk dto
                else
                    let! dto = success.Content |> Serializer.parseContent<'dto>
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
        }

    let get<'dto> uri client : AsyncHttpClientResponse<'dto> =
        client |> sendRequest<'dto> (fun client url -> client.GetAsync(url) |> Async.AwaitTask) uri

    let post<'request, 'response> uri (request: 'request) client : AsyncHttpClientResponse<'response> =
        injectedAsync {
            let! requestContent = request |> Serializer.toContent
            return! client |> sendRequest<'response> (fun client url -> client.PostAsync(url, requestContent) |> Async.AwaitTask) uri
        }

    let put<'request> uri request client : AsyncHttpClientResponse<unit> =
        injectedAsync {
            let! requestContent = request |> Serializer.toContent
            return! client |> sendRequest<unit> (fun client url -> client.PutAsync(url, requestContent) |> Async.AwaitTask) uri
        }

    let patch<'request> uri request client : AsyncHttpClientResponse<unit> =
        injectedAsync {
            let! requestContent = request |> Serializer.toContent
            return! client |> sendRequest<unit> (fun client url -> 
                use request = new HttpRequestMessage(HttpMethod("PATCH"), url)
                request.Content <- requestContent
                client.SendAsync(request) |> Async.AwaitTask) uri
        }

    let delete uri client : AsyncHttpClientResponse<unit> =
        client |> sendRequest<unit> (fun client url -> client.DeleteAsync(url) |> Async.AwaitTask) uri
