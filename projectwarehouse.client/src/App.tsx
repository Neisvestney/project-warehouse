import {useRegisterSW} from "virtual:pwa-register/react";
import {Route, Routes} from "react-router";
import CssBaseline from "@mui/material/CssBaseline";
import {ThemeProvider} from "@mui/material";
import theme from "@/theme.ts";
import MainLayout from "@/layouts/MainLayout/MainLayout.tsx";
import {SnackbarProvider} from "notistack";
import ServiceWorkerContext from "@/contexts/ServiceWorkerContext.ts";
import UpdatePrompt from "@/components/UpdatePromt/UpdatePrompt.tsx";
import React, {Suspense} from "react";

const HomePage = React.lazy(() => import("@/pages/HomePage/HomePage.tsx"));
const ScannerPage = React.lazy(() => import("@/pages/ScannerPage/ScannerPage.tsx"));

function App() {
  const {
    needRefresh: [needRefresh],
    offlineReady: [offlineReady],
    updateServiceWorker,
  } = useRegisterSW({
    onOfflineReady: () => {
      console.log("onOfflineReady");
    },
  });

  return (
    <ServiceWorkerContext.Provider value={{needRefresh, offlineReady, updateServiceWorker}}>
      <ThemeProvider theme={theme}>
        <SnackbarProvider>
          <CssBaseline />
          <UpdatePrompt />
          {/*{needRefresh && (*/}
          {/*  <div className="update-banner">*/}
          {/*    <p>Доступна новая версия. Требуется обновления</p>*/}
          {/*    <button onClick={() => updateServiceWorker()}>Обновить</button>*/}
          {/*  </div>*/}
          {/*)}*/}
          <Suspense>
            <Routes>
              <Route element={<MainLayout />}>
                <Route path="/" element={<HomePage />} />
              </Route>
              <Route path="/scanner" element={<ScannerPage />} />
            </Routes>
          </Suspense>
        </SnackbarProvider>
      </ThemeProvider>
    </ServiceWorkerContext.Provider>
  );
}

export default App;
