import type {DataFileLinkDto, DataFileLinkRequest, DataFileDto} from "@/api";

/**
 * Form-local shape for one element of an attachment list. Shared by every 1:N attachment point
 * (receipts, write-offs, orders, stocktakes, …) so the create/edit forms don't each invent their own.
 */
export type AttachmentLinkValue = {
  /** Id of the join row, absent until the attachment is saved with the owner entity. */
  entityId?: string;
  file: DataFileDto;
};

export function toAttachmentLinks(dtos: readonly DataFileLinkDto[]): AttachmentLinkValue[] {
  return dtos.map((d) => ({entityId: d.id, file: d.file}));
}

/** Order is the array index, so a drag-reorder needs no extra state. */
export function mapAttachmentsToRequest(links: AttachmentLinkValue[]): DataFileLinkRequest[] {
  return links.map((link, index) => ({
    id: link.entityId ?? null,
    fileId: link.file.id,
    order: index,
  }));
}
