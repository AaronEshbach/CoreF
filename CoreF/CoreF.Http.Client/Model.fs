namespace CoreF.Http.Client

open CoreF.DependencyInjection
open CoreF.Serialization
open System
open System.Net.Http

type HttpClientError =
| Unauthorized
| Forbidden
| NotFound
| NotAcceptable
| BadRequest of HttpResponseMessage
| UnprocessableEntity of HttpResponseMessage
| OtherClientError of (int * HttpResponseMessage)

type HttpClientResponse<'t> =
| HttpOk of 't
| HttpRedirect of Uri
| HttpClientError of HttpClientError
| HttpServerError of HttpResponseMessage

type HttpClientCallError<'e> =
| InvalidRequestUri
| DeserializationError of SerializationError
| ClientProcessingError of 'e
| UnexpectedHttpClientError of exn

type NoClientProcessingError = NoClientProcessingError

type HttpClientCall<'t, 'e> = HttpClientCall of InjectedAsync<HttpClientResponse<'t>, HttpClientCallError<'e>>

type HttpErrorResponse =
    | ClientError of HttpClientError
    | ServerError of HttpResponseMessage
    member this.Response =
        match this with
        | ClientError error ->
            match error with
            | Unauthorized -> None
            | Forbidden -> None
            | NotFound -> None
            | NotAcceptable -> None
            | BadRequest response -> Some response
            | UnprocessableEntity response -> Some response
            | OtherClientError (_, response) -> Some response
        | ServerError response ->
            Some response
    member this.StatusCode =
        match this with
        | ClientError error ->
            match error with
            | Unauthorized -> 401
            | Forbidden -> 403
            | NotFound -> 404
            | NotAcceptable -> 406
            | BadRequest _ -> 400
            | UnprocessableEntity _ -> 422
            | OtherClientError (status, _) -> status
        | ServerError response ->
            response.StatusCode |> int


[<AutoOpen; CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module HttpClientResponse =
    let (|HttpError|_|) (response: HttpClientResponse<_>) =
        match response with
        | HttpClientError error ->
            Some <| ClientError error
        | HttpServerError response ->
            Some <| ServerError response
        | _ -> 
            None