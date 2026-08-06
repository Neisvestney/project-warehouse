import type {DataFileDto} from "@/api";

/**
 * Anything the viewer can display. External sources exist because some images live elsewhere
 * (marketplace card thumbnails), and mixed lists must be scrollable in one gallery.
 */
export type ViewableFile =
  | {kind: "dataFile"; file: DataFileDto}
  | {kind: "external"; url: string; name?: string; contentType?: string};

export const viewable = (file: DataFileDto): ViewableFile => ({kind: "dataFile", file});

export const viewableUrl = (
  url: string,
  opts?: {name?: string; contentType?: string},
): ViewableFile => ({kind: "external", url, ...opts});
