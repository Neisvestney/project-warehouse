// Read by apiClient's request interceptor to tag mutating requests with the tab's SSE connection id,
// so the server can skip this tab (and only this tab) when fanning the resulting event back out.
let currentConnectionId: string | null = null;

export function setCurrentConnectionId(id: string | null) {
  currentConnectionId = id;
}

export function getCurrentConnectionId(): string | null {
  return currentConnectionId;
}
