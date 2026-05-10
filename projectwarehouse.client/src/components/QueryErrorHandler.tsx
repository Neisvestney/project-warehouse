import {useEffect} from "react";
import {useQueryClient} from "@tanstack/react-query";
import {useModal} from "@/hooks/useModal";
import {extractErrorMessage} from "@/utils/errorUtils";

export function QueryErrorHandler() {
  const queryClient = useQueryClient();
  const {showAlert} = useModal();

  useEffect(() => {
    const unsubQuery = queryClient.getQueryCache().subscribe((event) => {
      if (event.type !== "updated") return;
      if (event.query.state.status !== "error") return;
      if (event.query.state.fetchStatus !== "idle") return;
      if (event.query.meta?.suppressGlobalError) return;
      showAlert({
        title: "Ошибка запроса",
        message: extractErrorMessage(event.query.state.error),
        severity: "error",
      });
    });

    const unsubMutation = queryClient.getMutationCache().subscribe((event) => {
      if (event.type !== "updated") return;
      if (event.mutation?.state.status !== "error") return;
      if (event.mutation.meta?.suppressGlobalError) return;
      showAlert({
        title: "Ошибка",
        message: extractErrorMessage(event.mutation.state.error),
        severity: "error",
      });
    });

    return () => {
      unsubQuery();
      unsubMutation();
    };
  }, [queryClient, showAlert]);

  return null;
}
