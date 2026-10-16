import type {AppProblemDetails} from "@/api/types.gen";
import {isAppProblemDetails} from "./errorUtils";

/**
 * With `parseAs: "blob"` the client hands back the *error* body as a Blob too, so resolveErrorMessage
 * cannot see it. Binary endpoints have to unwrap it by hand.
 */
export async function parseProblemFromBlob(error: unknown): Promise<AppProblemDetails | null> {
  if (!(error instanceof Blob)) return null;

  try {
    const parsed: unknown = JSON.parse(await error.text());
    return isAppProblemDetails(parsed) ? parsed : null;
  } catch {
    return null;
  }
}
