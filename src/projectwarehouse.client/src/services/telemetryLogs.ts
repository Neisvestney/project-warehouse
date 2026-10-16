import {SeverityNumber, type LogAttributes} from "@opentelemetry/api-logs";
import {ATTR_APP_PAGE, getCurrentPage} from "@/services/currentPage";
import {getCurrentUserAttributes} from "@/services/currentUser";

export interface ClientLogRecord {
  timestamp: number;
  severityNumber: SeverityNumber;
  severityText: string;
  body: string;
  attributes: LogAttributes;
}

export type ClientLogSink = (record: ClientLogRecord) => void;

/**
 * The proxy endpoint needs a token, so nothing can be shipped before login. Records wait here and
 * are drained once a sink is attached; a user who never logs in leaves no telemetry behind.
 */
const BUFFER_LIMIT = 100;
const buffer: ClientLogRecord[] = [];

let sink: ClientLogSink | null = null;
// Guards synchronous re-entry only: a sink that logs while handling a record would otherwise recurse
// on itself. It says nothing about the exporter's own failures — those are reported through the
// OpenTelemetry diag logger, which stays a no-op, so they never reach the console wrapper below.
let delivering = false;

function deliver(record: ClientLogRecord, to: ClientLogSink) {
  delivering = true;
  try {
    to(record);
  } finally {
    delivering = false;
  }
}

export function emitClientLog(input: ClientLogRecord) {
  if (delivering) return;

  // Stamped here rather than at delivery: a record that waited in the buffer must keep the screen it
  // was captured on and the user who was signed in then, not whoever the sink finds later.
  const record: ClientLogRecord = {
    ...input,
    attributes: {
      [ATTR_APP_PAGE]: getCurrentPage(),
      ...getCurrentUserAttributes(),
      ...input.attributes,
    },
  };

  if (sink) {
    deliver(record, sink);
    return;
  }
  if (buffer.length >= BUFFER_LIMIT) buffer.shift();
  buffer.push(record);
}

/** Attaches the exporter-backed sink and drains everything captured before authentication. */
export function setClientLogSink(next: ClientLogSink) {
  sink = next;
  const pending = buffer.splice(0, buffer.length);
  for (const record of pending) deliver(record, next);
}

/** Detaches the sink on logout — subsequent records go back into the buffer. */
export function clearClientLogSink() {
  sink = null;
}

function record(
  severityNumber: SeverityNumber,
  severityText: string,
  body: string,
  attributes: LogAttributes = {},
) {
  emitClientLog({timestamp: Date.now(), severityNumber, severityText, body, attributes});
}

export function logClientEvent(body: string, attributes: LogAttributes = {}) {
  record(SeverityNumber.INFO, "INFO", body, attributes);
}

export function logClientWarning(body: string, attributes: LogAttributes = {}) {
  record(SeverityNumber.WARN, "WARN", body, attributes);
}

export function logClientError(body: string, attributes: LogAttributes = {}) {
  record(SeverityNumber.ERROR, "ERROR", body, attributes);
}

function describe(value: unknown): string {
  if (value instanceof Error) return `${value.name}: ${value.message}`;
  if (typeof value === "string") return value;
  try {
    return JSON.stringify(value) ?? String(value);
  } catch {
    return String(value);
  }
}

function errorAttributes(value: unknown): LogAttributes {
  if (!(value instanceof Error)) return {};
  return {
    "exception.type": value.name,
    "exception.message": value.message,
    ...(value.stack ? {"exception.stacktrace": value.stack} : {}),
  };
}

let capturing = false;

/**
 * Starts collecting unhandled errors and console noise. Called from `main.tsx` before the telemetry
 * SDK is loaded, so the records of the very first seconds are not lost.
 */
export function installClientLogCapture() {
  if (capturing) return;
  capturing = true;

  window.addEventListener("error", (event) => {
    // A failed <img>/<script> raises an error event with neither field set — nothing to report.
    if (!event.error && !event.message) return;

    logClientError(describe(event.error ?? event.message), {
      "code.filepath": event.filename,
      "code.lineno": event.lineno,
      ...errorAttributes(event.error),
    });
  });

  window.addEventListener("unhandledrejection", (event) => {
    logClientError(describe(event.reason), {
      "exception.escaped": true,
      ...errorAttributes(event.reason),
    });
  });

  const originalWarn = console.warn.bind(console);
  const originalError = console.error.bind(console);

  console.warn = (...args: unknown[]) => {
    originalWarn(...args);
    logClientWarning(args.map(describe).join(" "));
  };

  console.error = (...args: unknown[]) => {
    originalError(...args);
    const cause = args.find((a) => a instanceof Error);
    logClientError(args.map(describe).join(" "), errorAttributes(cause));
  };
}
