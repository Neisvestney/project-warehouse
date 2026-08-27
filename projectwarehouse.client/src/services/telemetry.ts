import {logs} from "@opentelemetry/api-logs";
import {OTLPLogExporter} from "@opentelemetry/exporter-logs-otlp-http";
import {OTLPTraceExporter} from "@opentelemetry/exporter-trace-otlp-http";
import {registerInstrumentations} from "@opentelemetry/instrumentation";
import {DocumentLoadInstrumentation} from "@opentelemetry/instrumentation-document-load";
import {FetchInstrumentation} from "@opentelemetry/instrumentation-fetch";
import {resourceFromAttributes} from "@opentelemetry/resources";
import {BatchLogRecordProcessor, LoggerProvider} from "@opentelemetry/sdk-logs";
import {
  BatchSpanProcessor,
  WebTracerProvider,
  type SpanProcessor,
} from "@opentelemetry/sdk-trace-web";
import {ATTR_SERVICE_NAME, ATTR_SERVICE_VERSION} from "@opentelemetry/semantic-conventions";
import {getFreshAccessToken} from "@/services/apiClient";
import {ATTR_APP_PAGE, getCurrentPage} from "@/services/currentPage";
import {getCurrentUserAttributes} from "@/services/currentUser";
import {clearClientLogSink, setClientLogSink} from "@/services/telemetryLogs";

const SESSION_ID_KEY = "telemetrySessionId";
const LOGGER_NAME = "projectwarehouse.client";

let initialized = false;

function escapeRegExp(value: string) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

/** Glues together the actions of one tab when there are many traces and it is unclear whose they are. */
function getSessionId(): string {
  const existing = sessionStorage.getItem(SESSION_ID_KEY);
  if (existing) return existing;

  const id = crypto.randomUUID();
  sessionStorage.setItem(SESSION_ID_KEY, id);
  return id;
}

// Taken at send time rather than at exporter construction: the token rotates while the tab lives.
const authHeaders = async (): Promise<Record<string, string>> => {
  const token = await getFreshAccessToken();
  return token ? {Authorization: `Bearer ${token}`} : {};
};

/**
 * Stamps every span with the screen it started on and the user who started it: the endpoint alone
 * says neither which of the pages that call it was open, nor whose session it was. On start rather
 * than on end, so a navigation or a logout during a long request does not rewrite the answer.
 */
const ambientAttributeProcessor: SpanProcessor = {
  onStart: (span) => {
    span.setAttribute(ATTR_APP_PAGE, getCurrentPage());
    span.setAttributes(getCurrentUserAttributes());
  },
  onEnd: () => {},
  forceFlush: () => Promise.resolve(),
  shutdown: () => Promise.resolve(),
};

export function initTelemetry() {
  if (initialized) return;
  initialized = true;

  const origin = window.location.origin;
  const resource = resourceFromAttributes({
    [ATTR_SERVICE_NAME]: "projectwarehouse.client",
    [ATTR_SERVICE_VERSION]: import.meta.env.VITE_APP_VERSION,
    "session.id": getSessionId(),
  });

  const tracerProvider = new WebTracerProvider({
    resource,
    spanProcessors: [
      ambientAttributeProcessor,
      new BatchSpanProcessor(
        new OTLPTraceExporter({
          url: `${origin}/api/telemetry/v1/traces`,
          headers: authHeaders,
        }),
      ),
    ],
  });
  tracerProvider.register();

  registerInstrumentations({
    tracerProvider,
    instrumentations: [
      new DocumentLoadInstrumentation(),
      new FetchInstrumentation({
        // traceparent must not leave for third-party domains
        propagateTraceHeaderCorsUrls: [new RegExp(`^${escapeRegExp(origin)}/api/`)],
        // /api/telemetry closes the recursion one step earlier than the collector's filter/noise:
        // otherwise sending a batch produces a span that lands in the next batch. The SSE stream
        // produces a span as long as the session, which is noise — its status is visible in
        // RealtimeProvider instead.
        ignoreUrls: [/\/api\/telemetry\//, /\/api\/realtime\/stream/],
        clearTimingResources: true,
      }),
    ],
  });

  const loggerProvider = new LoggerProvider({
    resource,
    processors: [
      new BatchLogRecordProcessor({
        exporter: new OTLPLogExporter({
          url: `${origin}/api/telemetry/v1/logs`,
          headers: authHeaders,
        }),
      }),
    ],
  });
  logs.setGlobalLoggerProvider(loggerProvider);

  const logger = logs.getLogger(LOGGER_NAME, import.meta.env.VITE_APP_VERSION);
  const syncLogSink = () => {
    if (localStorage.getItem("accessToken")) {
      setClientLogSink((record) =>
        logger.emit({
          timestamp: record.timestamp,
          severityNumber: record.severityNumber,
          severityText: record.severityText,
          body: record.body,
          attributes: record.attributes,
        }),
      );
    } else {
      clearClientLogSink();
    }
  };

  window.addEventListener("auth:tokens", syncLogSink);
  window.addEventListener("auth:clear", syncLogSink);
  syncLogSink();
}
