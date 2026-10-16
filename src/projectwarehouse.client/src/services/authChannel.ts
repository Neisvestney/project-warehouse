// Cross-tab auth transport. Tokens already live in localStorage, which every tab of the origin shares,
// so a message here carries no payload — it only tells the other tabs that storage changed and which
// local `auth:*` event they should raise. Receivers never write storage back, so no echo is possible.

export type AuthBroadcast = "tokens" | "clear";

const CHANNEL_NAME = "auth";

// Older WebViews have no BroadcastChannel; there a storage write is the transport. The nonce keeps two
// identical events distinguishable, since `storage` only fires when the value actually differs.
const FALLBACK_KEY = "auth:broadcast";

const channel = typeof BroadcastChannel === "undefined" ? null : new BroadcastChannel(CHANNEL_NAME);

function receive(type: AuthBroadcast) {
  window.dispatchEvent(new Event(type === "tokens" ? "auth:tokens" : "auth:clear"));
}

let listening = false;

export function setupAuthChannel() {
  // Idempotent: a second call (HMR, a re-init of the client) would otherwise double every auth event.
  if (listening) return;
  listening = true;

  if (channel) {
    channel.addEventListener("message", (event: MessageEvent<AuthBroadcast>) =>
      receive(event.data),
    );
    return;
  }

  window.addEventListener("storage", (event) => {
    if (event.key !== FALLBACK_KEY || !event.newValue) return;
    try {
      receive(JSON.parse(event.newValue).type as AuthBroadcast);
    } catch {
      // Anything can write to localStorage; a malformed value is not ours to act on.
    }
  });
}

// The fallback key is a transport, not state: drop it on logout so no auth artefact outlives the
// session. Removal raises `storage` with a null value, which receivers ignore.
export function clearAuthChannelStorage() {
  if (!channel) localStorage.removeItem(FALLBACK_KEY);
}

export function broadcastAuth(type: AuthBroadcast) {
  if (channel) {
    channel.postMessage(type);
    return;
  }
  localStorage.setItem(FALLBACK_KEY, JSON.stringify({type, nonce: Date.now() + Math.random()}));
}
