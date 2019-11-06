namespace CoreF.Http

open CoreF.Common
open CoreF.Mapping
open System

[<RequireQualifiedAccess>]
type HttpMethod =
| Get 
| Put
| Post
| Patch
| Delete
| Options
| Head
| Custom of string

[<Struct>] type RequestId = private RequestId of Guid

module RequestId =
    let create () = 
        Guid.NewGuid() |> RequestId

    let tryParse (id: string) =
        tryParse<Guid> id 
        |> Option.map RequestId

    let value (RequestId id) = 
        id |> sprintf "%A"

    let internal guidValue (RequestId id) = id
        
type IHttpClientMetadata =
    abstract member Host: string
    abstract member User: string

type IHttpRequest =
    abstract member RequestId: RequestId
    abstract member Headers: Map<CaseInsensitiveString, string>
    abstract member Method: HttpMethod
    abstract member Url: Uri
    abstract member Body: byte []
    abstract member Client: IHttpClientMetadata

type HttpRequestTemplateError =
| WrongVerb
| WrongNumberOfSegments
| RouteDoesNotMatch
| InvalidUrl

type HttpParameterValidationError =
| MissingRequestBody
| RequestBodyMustBeByteArrayOrString
| ParameterNotFound of string
| ErrorDeserializingRequest of exn
| NoDtoMappingFunctionFound of Type * Type
| ErrorMappingValidatedObject of MappingError
| ErrorFindingParameterMapping of (Type * Type) * MappingError
| UrlParametersMustNotBeDTOs of Type
| UnsupportedContentType of string
| UnsupportedParameterType of Type
| UnsupportedValidationType of Type
| UnsupportedMetadataParameterType of Type
| ParameterDomainValidationError of string []
| ArrayDeserializationError of HttpParameterValidationError list
| UnhandledErrorInspectingParameterValidationResult of exn * obj

type HttpRequestParameterError =
| MissingRequiredParameters of HttpParameterValidationError list
| InvalidRequestBodyType
| ParameterValidationFailed of HttpParameterValidationError list

type HttpRequestEntryPointError =
| MissingHttpAttribute
| UnsupportedEntryPointType of Type

type HttpRequestContextError = 
| WrongApiVersion of string
| WrongBoundedContext of string

type MatchHttpRequestError =
| HttpRequestTemplateDoesNotMatch of HttpRequestTemplateError
| EntryPointParametersDoNotMatch of HttpRequestParameterError
| WrongRequestContext of HttpRequestContextError
| InvalidEntryPoint of HttpRequestEntryPointError

type HttpRequestError =
| HttpHandlerMethodMustBeStaticOrClassMustHaveDefaultConstructor
| ErrorMatchingAvailableEntryPoints of MatchHttpRequestError
| RuntimeParameterValidationFailed of HttpParameterValidationError list
| NoMatchingEntryPointsFound
| UnsupportedReturnType of Type
| EntryPointTypeNotSupported of Type
| UnhandledExceptionExecutingRequest of exn


module HttpMethod =
    let parse (httpMethod: string) =
        match httpMethod with
        | Like "GET" -> HttpMethod.Get
        | Like "PUT" -> HttpMethod.Put
        | Like "POST" -> HttpMethod.Post
        | Like "PATCH" -> HttpMethod.Patch
        | Like "DELETE" -> HttpMethod.Delete
        | Like "OPTIONS" -> HttpMethod.Options
        | Like "HEAD" -> HttpMethod.Head
        | custom -> HttpMethod.Custom custom

    let toString = function
    | HttpMethod.Get -> "GET"
    | HttpMethod.Put -> "PUT"
    | HttpMethod.Post -> "POST"
    | HttpMethod.Patch -> "PATCH"
    | HttpMethod.Delete -> "DELETE"
    | HttpMethod.Options -> "OPTIONS"
    | HttpMethod.Head -> "HEAD"
    | HttpMethod.Custom other -> other

[<AutoOpen>]
[<CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Constants =
    [<Literal>] 
    let TraceContext = "traceparent"

    [<Literal>]
    let NoCache = "no-cache"
