export function fetchWithTimeout(url: string, timeoutMs: number): Promise<Response> {
  const timeout = new Promise<never>((_, reject) =>
    setTimeout(() => reject(new Error("timeout")), timeoutMs),
  );
  return Promise.race([fetch(url), timeout]);
}
