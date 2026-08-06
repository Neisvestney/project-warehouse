import {useMutation} from "@tanstack/react-query";
import {filesUploadMutation} from "@/api/@tanstack/react-query.gen";
import type {DataFileDto} from "@/api";

/**
 * Upload-first: a file is sent as soon as it is picked and the form afterwards holds a DataFileDto.
 * Files nothing ends up referencing are removed by the server's garbage collector.
 */
export function useFileUpload(options?: {onUploaded?: (file: DataFileDto) => void}) {
  const mutation = useMutation({
    ...filesUploadMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: (data) => options?.onUploaded?.(data),
  });

  const upload = (file: File) => mutation.mutateAsync({body: {file}});

  return {
    upload,
    isUploading: mutation.isPending,
    error: mutation.error,
    reset: mutation.reset,
  };
}
