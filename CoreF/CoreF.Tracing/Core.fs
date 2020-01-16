namespace CoreF.Tracing

open OpenTracing

type TracingError =
| OpenTracingNotConfigured
| InvalidTraceProviderType of System.Type
| UnexpectedTracingError of exn

type TraceLevel =
| Verbose
| Detailed
| Normal
| Minimal

type IDistributedTracing =
    abstract member Tracer: ITracer
    abstract member Enabled: bool
    abstract member Level: TraceLevel
