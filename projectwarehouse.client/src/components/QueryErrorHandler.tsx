import {useEffect, useRef} from "react";
import {useQueryClient} from "@tanstack/react-query";
import {useModal} from "@/hooks/useModal";
import {extractErrorMessage} from "@/utils/errorUtils";

export function QueryErrorHandler() {
  const queryClient = useQueryClient();
  const {showAlert} = useModal();
  const openMessages = useRef(new Set<string>());

  useEffect(() => {
    const showIfNew = (message: string, title: string) => {
      if (openMessages.current.has(message)) return;
      openMessages.current.add(message);
      showAlert({title, message, severity: "error"}).then(() => {
        openMessages.current.delete(message);
      });
    };

    const unsubQuery = queryClient.getQueryCache().subscribe((event) => {
      if (event.type !== "updated") return;
      if (event.query.state.status !== "error") return;
      if (event.query.state.fetchStatus !== "idle") return;
      if (event.query.meta?.suppressGlobalError) return;
      showIfNew(extractErrorMessage(event.query.state.error), "Ошибка запроса");
    });

    const unsubMutation = queryClient.getMutationCache().subscribe((event) => {
      if (event.type !== "updated") return;
      if (event.mutation?.state.status !== "error") return;
      if (event.mutation.meta?.suppressGlobalError) return;
      showIfNew(extractErrorMessage(event.mutation.state.error), "Ошибка");
    });

    return () => {
      unsubQuery();
      unsubMutation();
    };
  }, [queryClient, showAlert]);

  useEffect(() => {
    const handler = () => {
      showAlert({
        title: "Ваша сессия истекла",
        message: "Пожалуйста, авторизуйтесь снова, чтобы продолжить работу",
        severity: "warning",
      });
    };

    window.addEventListener("auth:refreshTokenInvalid", handler);
    return () => window.removeEventListener("auth:refreshTokenInvalid", handler);
  }, [showAlert]);

  return null;
}
