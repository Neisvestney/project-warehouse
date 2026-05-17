import {ResizeObserver as ResizeObserverPolyfill} from "@juggle/resize-observer";
if (!window.ResizeObserver) {
  window.ResizeObserver = ResizeObserverPolyfill;
}

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

setupApiClient();

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {retry: false},
    mutations: {retry: false},
  },
});

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <BrowserRouter>
      <QueryClientProvider client={queryClient}>
        <App />
      </QueryClientProvider>
    </BrowserRouter>
  </StrictMode>,
);
