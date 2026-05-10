import {useState, useCallback} from "react";
import {useModal} from "@/hooks/useModal";
import {isAppProblemDetails, errorCodeMessages, extractErrorMessage} from "@/utils/errorUtils";

function joinErrors(errs: {code: string; detail: string}[]): string {
  return errs
    .map((e) => errorCodeMessages[e.code as keyof typeof errorCodeMessages] ?? e.detail)
    .join(", ");
}

export function useFormErrors<TFields extends string>() {
  const {showAlert} = useModal();
  const [fieldErrors, setFieldErrors] = useState<Partial<Record<TFields, string>>>({});
  const [rootError, setRootError] = useState<string | null>(null);

  const setApiError = useCallback(
    (error: unknown) => {
      if (isAppProblemDetails(error)) {
        const newFieldErrors: Partial<Record<TFields, string>> = {};
        for (const [field, errs] of Object.entries(error.errors)) {
          if (field !== "root" && errs.length > 0) {
            newFieldErrors[field as TFields] = joinErrors(errs);
          }
        }
        setFieldErrors(newFieldErrors);

        const rootErrs = error.errors["root"];
        if (rootErrs?.length) {
          setRootError(joinErrors(rootErrs));
        } else if (Object.keys(newFieldErrors).length === 0) {
          setRootError(error.title ?? error.detail ?? "Неизвестная ошибка");
        } else {
          setRootError(null);
        }
      } else {
        showAlert({title: "Ошибка", message: extractErrorMessage(error), severity: "error"});
      }
    },
    [showAlert],
  );

  const clearFieldError = useCallback((field: TFields) => {
    setFieldErrors((prev) => {
      const next = {...prev};
      delete next[field];
      return next;
    });
  }, []);

  const clearRootError = useCallback(() => setRootError(null), []);

  const clearErrors = useCallback(() => {
    setFieldErrors({});
    setRootError(null);
  }, []);

  return {fieldErrors, rootError, setApiError, clearFieldError, clearRootError, clearErrors};
}
