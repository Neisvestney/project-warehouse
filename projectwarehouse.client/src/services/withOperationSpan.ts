import {SpanKind, SpanStatusCode, context, trace, type Attributes} from "@opentelemetry/api";

const TRACER_NAME = "projectwarehouse.client.operations";

/**
 * Spread into the options of every request that belongs to the operation:
 * `await postReceipt({path: {id}, ...op})`. A request that does not get it still runs, but its span
 * ends up in a trace of its own.
 */
export interface OperationScope {
  fetch: typeof fetch;
}

/**
 * Wraps a warehouse-state-changing operation in a span. Requests given the `op` scope become its
 * children and carry its `traceparent` to the backend.
 *
 * Imports nothing but `@opentelemetry/api`, so it works before the telemetry SDK has loaded:
 * without a registered provider the tracer is a no-op and the callback runs untouched.
 */
export async function withOperationSpan<T>(
  name: string,
  attributes: Attributes,
  run: (op: OperationScope) => Promise<T>,
): Promise<T> {
  const options = {attributes, kind: SpanKind.CLIENT};

  return trace.getTracer(TRACER_NAME).startActiveSpan(name, options, async (span) => {
    // Captured while the context is still active — `StackContextManager` restores it as soon as the
    // callback hits its first await, long before the client reaches fetch. Travelling as a value
    // instead of ambient state, it is re-entered at the one instant the instrumentation reads it,
    // which is also what keeps parallel requests from stepping on each other.
    const ctx = context.active();
    const op: OperationScope = {
      fetch: (input, init) => context.with(ctx, () => globalThis.fetch(input, init)),
    };

    try {
      return await run(op);
    } catch (error) {
      span.recordException(error as Error);
      span.setStatus({
        code: SpanStatusCode.ERROR,
        message: error instanceof Error ? error.message : String(error),
      });
      throw error;
    } finally {
      span.end();
    }
  });
}
