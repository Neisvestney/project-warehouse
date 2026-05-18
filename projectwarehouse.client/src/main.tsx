import "abortcontroller-polyfill/dist/polyfill-patch-fetch";
import {ResizeObserver as ResizeObserverPolyfill} from "@juggle/resize-observer";
if (!window.ResizeObserver) {
  window.ResizeObserver = ResizeObserverPolyfill;
}

// Chrome < 88 doesn't support complex selectors inside :not() — strip them and retry.
// Needed for MUI X TreeView which generates selectors like [role="treeitem"]:not(* [role="treeitem"] [role="treeitem"]).
const _origQSA = Element.prototype.querySelectorAll;
Element.prototype.querySelectorAll = function (this: Element, selector: string) {
  try {
    return _origQSA.call(this, selector);
  } catch {
    return _origQSA.call(this, selector.replace(/:not\([^)]*\)/g, ""));
  }
} as typeof Element.prototype.querySelectorAll;

import {StrictMode} from "react";
import {createRoot} from "react-dom/client";
import "./index.css";
import App from "@/App.tsx";
import {BrowserRouter} from "react-router";
import {QueryClient, QueryClientProvider} from "@tanstack/react-query";
import "@fontsource/roboto/300.css";
import "@fontsource/roboto/400.css";
import "@fontsource/roboto/500.css";
import "@fontsource/roboto/700.css";
import {setupApiClient} from "@/services/apiClient.ts";
import {Capacitor} from "@capacitor/core";
import {SELECTED_SERVER_KEY} from "@/configuration/servers.ts";
import {fetchWithTimeout} from "@/utils/fetchWithTimeout.ts";

setupApiClient();

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {retry: false},
    mutations: {retry: false},
  },
});

function mountApp() {
  createRoot(document.getElementById("root")!).render(
    <StrictMode>
      <BrowserRouter>
        <QueryClientProvider client={queryClient}>
          <App />
        </QueryClientProvider>
      </BrowserRouter>
    </StrictMode>,
  );
}

const LAUNCHER_ORIGINS = ["capacitor://localhost", "https://localhost"];

if (new URLSearchParams(window.location.search).get("clear_server") === "1") {
  localStorage.removeItem(SELECTED_SERVER_KEY);
  window.history.replaceState({}, "", "/");
}

if (Capacitor.isNativePlatform() && LAUNCHER_ORIGINS.includes(window.location.origin)) {
  const savedUrl = localStorage.getItem(SELECTED_SERVER_KEY);
  if (savedUrl) {
    const loader = document.createElement("div");
    loader.style.cssText =
      "position:fixed;inset:0;display:flex;align-items:center;justify-content:center;background:#fff;font-family:sans-serif;font-size:16px;color:#555";
    loader.textContent = "Подключение к серверу…";
    document.body.appendChild(loader);

    const healthUrl = savedUrl.replace(/\/$/, "") + "/health";
    fetchWithTimeout(healthUrl, 5000)
      .then((res) => {
        if (!res.ok) throw new Error();
        document.body.removeChild(loader);
        window.location.href = savedUrl;
      })
      .catch(() => {
        localStorage.removeItem(SELECTED_SERVER_KEY);
        document.body.removeChild(loader);
        mountApp();
      });
  } else {
    mountApp();
  }
} else {
  mountApp();
}
