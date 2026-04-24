import React from "react";

interface ServiceWorkerContext {
  needRefresh: boolean;
  offlineReady: boolean;
  updateServiceWorker: (reloadPage?: boolean) => Promise<void>;
}

const ServiceWorkerContext = React.createContext<ServiceWorkerContext>(null!);

export default ServiceWorkerContext;
