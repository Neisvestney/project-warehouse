import {useState} from "react";
import {useQueryClient} from "@tanstack/react-query";
import {ordersGetAllQueryKey, ordersGetByIdQueryKey} from "@/api/@tanstack/react-query.gen";
import {ordersGetLabels} from "@/api/sdk.gen";
import type {OrderLabelsGrouping} from "@/api/types.gen";
import {reserveBlobTab, withTimestamp} from "@/utils/downloadUtils";
import {parseProblemFromBlob} from "@/utils/blobErrorUtils";
import {extractErrorMessage, firstFieldError, resolveErrorMessage} from "@/utils/errorUtils";

export interface LabelsError {
  message: string;
  /** Postings the server named as the reason — shown as a list, not squeezed into the message. */
  postingNumbers: string[];
}

interface DownloadLabelsOptions {
  orderIds: string[];
  grouping?: OrderLabelsGrouping;
  fileName?: string;
}

function readPostingNumbers(args: Record<string, unknown> | null | undefined): string[] {
  const value = args?.["postingNumbers"];
  if (!Array.isArray(value)) return [];
  return value.filter((item): item is string => typeof item === "string");
}

/** Shared by the bulk button on the list and the single-order button on the order page. */
export function useDownloadLabels() {
  const queryClient = useQueryClient();
  const [isPending, setIsPending] = useState(false);
  const [error, setError] = useState<LabelsError | null>(null);

  async function download({
    orderIds,
    grouping = "none",
    fileName = "labels.pdf",
  }: DownloadLabelsOptions): Promise<boolean> {
    setIsPending(true);
    setError(null);
    // the click is still the current gesture here — after the request it no longer is
    const tab = reserveBlobTab();
    try {
      // The generated *Options helpers mistype binary endpoints, so the SDK function is called
      // directly with parseAs: "blob" — same approach as useFileBlobUrl.
      const response = await ordersGetLabels({
        body: {orderIds, grouping},
        parseAs: "blob",
        throwOnError: false,
      });

      if (response.error !== undefined) {
        tab.close();
        const problem = (await parseProblemFromBlob(response.error)) ?? response.error;
        const fieldError =
          typeof problem === "object" && problem !== null && "errors" in problem
            ? firstFieldError(problem as never)
            : undefined;
        setError({
          message: fieldError ? resolveErrorMessage(fieldError) : extractErrorMessage(problem),
          postingNumbers: readPostingNumbers(fieldError?.args),
        });
        return false;
      }

      tab.show(response.data as unknown as Blob, withTimestamp(fileName));

      // printing fills LabelFileId, and the button states read it
      await queryClient.invalidateQueries({queryKey: ordersGetAllQueryKey()});
      await Promise.all(
        orderIds.map((id) =>
          queryClient.invalidateQueries({queryKey: ordersGetByIdQueryKey({path: {id}})}),
        ),
      );
      return true;
    } catch (e) {
      tab.close();
      setError({message: extractErrorMessage(e), postingNumbers: []});
      return false;
    } finally {
      setIsPending(false);
    }
  }

  return {download, isPending, error, clearError: () => setError(null)};
}
