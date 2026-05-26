import {useCallback} from "react";
import type {FieldValues, Path, UseFormReturn} from "react-hook-form";
import {useModal} from "@/hooks/useModal";
import {isAppProblemDetails, resolveErrorMessage, extractErrorMessage} from "@/utils/errorUtils";
import type {AppFieldError} from "@/api/types.gen";

function joinErrors(errs: AppFieldError[]): string {
  return errs.map(resolveErrorMessage).join(", ");
}

export function useRhfApiErrors<T extends FieldValues>(form: UseFormReturn<T>) {
  const {showAlert} = useModal();

  const setApiError = useCallback(
    (error: unknown) => {
      if (isAppProblemDetails(error)) {
        let hasFields = false;
        for (const [rawField, errs] of Object.entries(error.errors)) {
          const field = rawField.replace(/\[(\d+)\]/g, ".$1");
          if (field !== "root" && errs.length > 0) {
            form.setError(field as Path<T>, {type: "server", message: joinErrors(errs)});
            hasFields = true;
          }
        }
        const rootErrs = error.errors["root"];
        if (rootErrs?.length) {
          form.setError("root" as Path<T>, {type: "server", message: joinErrors(rootErrs)});
        } else if (!hasFields) {
          form.setError("root" as Path<T>, {
            type: "server",
            message: error.title ?? "Неизвестная ошибка",
          });
        }
      } else {
        showAlert({title: "Ошибка", message: extractErrorMessage(error), severity: "error"});
      }
    },
    [form, showAlert],
  );

  return {setApiError};
}
