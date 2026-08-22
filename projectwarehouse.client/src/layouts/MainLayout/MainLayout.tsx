import React, {Suspense} from "react";
import {Outlet} from "react-router";
import {RealtimeProvider} from "@/contexts/Realtime/RealtimeProvider";
import {SearchParamsProvider} from "@/contexts/SearchParams/SearchParamsProvider";
import ServiceWorkerUpdateWatcher from "@/components/ServiceWorkerUpdateWatcher.tsx";
import RouteFallback from "@/components/RouteFallback.tsx";

export interface MainLayoutProps {}

function MainLayout({}: MainLayoutProps) {
  return (
    <RealtimeProvider>
      <ServiceWorkerUpdateWatcher />
      <SearchParamsProvider>
        {/* Below the providers: a boundary above them would tear down the stream on every lazy chunk. */}
        <Suspense fallback={<RouteFallback />}>
          <Outlet />
        </Suspense>
      </SearchParamsProvider>
    </RealtimeProvider>
  );
}

export default MainLayout;
