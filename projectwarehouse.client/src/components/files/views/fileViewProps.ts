import type {DataFileDto} from "@/api";

/** Shared contract of the view layer. Pure display — no API access. */
export interface FileViewProps {
  file: DataFileDto;
  loading?: boolean;
  disabled?: boolean;
  onDelete?: () => void;
  onReplace?: () => void;
  onOpen?: () => void;
}
