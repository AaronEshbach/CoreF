namespace CoreF.Http.Auditing

open CoreF.Common
open System

type HttpClientMetadata =
    {
        Host: string
        User: string
    }

type HttpRequest =
    {
        RequestId: string
        Headers: Map<string, string>
        Method: string
        Url: Uri
        Body: byte []
        Client: HttpClientMetadata
        Server: string
        Timestamp: DateTime
    }

type HttpResponse =
    {
        RequestId: string
        Headers: Map<string, string>
        StatusCode: int
        Body: byte []
        Timestamp: DateTime
        ElapsedTime: TimeSpan
    }

type AuditingError =
| AuditingConfigurationNotFound
| ErrorCollectingAuditData of HttpRequest * exn
| UnhandledAuditingError of exn

type IHttpAuditor =
    inherit IDisposable
    abstract member Enabled: bool
    abstract member NumberOfAgents: int
    abstract member AuditRequest: HttpRequest -> AsyncResult<unit, AuditingError> 
    abstract member AuditResponse: HttpResponse -> AsyncResult<unit, AuditingError>