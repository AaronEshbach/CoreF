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

type HttpClientCallError =
| InvalidRequestUri
| SerializationError of SerializationError
| DeserializationError of SerializationError
| UnexpectedHttpClientError of exn

type HttpClientCall<'t> = HttpClientCall of InjectedAsync<HttpClientResponse<'t>, HttpClientCallError>