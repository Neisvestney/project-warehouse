import {useCallback, useEffect, useMemo, useState} from "react";
import type {Control, FieldPath, UseFormSetValue} from "react-hook-form";
import {Controller, useFieldArray, useForm, useWatch} from "react-hook-form";
import {useMutation, useQueries, useQuery, useQueryClient} from "@tanstack/react-query";
import {useSnackbar} from "notistack";
import {
  Alert,
  Autocomplete,
  Box,
  Button,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Divider,
  Drawer,
  FormControlLabel,
  IconButton,
  MenuItem,
  Radio,
  RadioGroup,
  Select,
  Stack,
  Switch,
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableRow,
  TextField,
  Tooltip,
  Typography,
} from "@mui/material";
import {byOperation} from "@/utils/queryKeys";
import {useEditLock} from "@/hooks/useEditLock";
import EditLockBanner from "@/components/EditLockBanner";
import EntityViewers from "@/components/EntityViewers";
import StaleDataBanner from "@/components/StaleDataBanner";
import AddIcon from "@mui/icons-material/Add";
import CloseIcon from "@mui/icons-material/Close";
import ContentCopyIcon from "@mui/icons-material/ContentCopy";
import DeleteIcon from "@mui/icons-material/Delete";
import EditIcon from "@mui/icons-material/Edit";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";
import PrintIcon from "@mui/icons-material/Print";
import {
  catalogCreateTagMutation,
  catalogDeleteMutation,
  catalogGetAllQueryKey,
  catalogGetByIdOptions,
  catalogGetByIdQueryKey,
  catalogGetTagsOptions,
  catalogUpdateMutation,
} from "@/api/@tanstack/react-query.gen";
import type {
  CatalogItemDto,
  CatalogItemSelectDto,
  CatalogItemTagDto,
  DataFileDto,
  UpdateCatalogItemRequest,
} from "@/api/types.gen";
import FileImage from "@/components/files/FileImage";
import FileListControl from "@/components/files/controls/FileListControl";
import SingleFileControl from "@/components/files/controls/SingleFileControl";
import AddFileInput from "@/components/files/inputs/AddFileInput";
import ImageCardFileView from "@/components/files/views/ImageCardFileView";
import ImagePreviewFileView from "@/components/files/views/ImagePreviewFileView";
import FileViewerModal from "@/components/files/viewer/FileViewerModal";
import {viewable} from "@/components/files/viewer/viewableFile";
import CatalogItemsSelect from "@/components/CatalogItemsSelect";
import CatalogItemTypeChip from "@/components/catalog/CatalogItemTypeChip";
import ConfirmDialog from "@/components/ConfirmDialog";
import NotFound from "@/components/NotFound";
import QueryError from "@/components/QueryError";
import LoadingOverlay from "@/components/LoadingOverlay";
import {FormTextField} from "@/components/form/FormTextField";
import {useHasPermission} from "@/hooks/usePermission";
import {useModal} from "@/hooks/useModal";
import {useRhfApiErrors} from "@/hooks/useRhfApiErrors";
import {useDebounce} from "@/hooks/useDebounce";
import {isNotFoundError} from "@/utils/errorUtils";
import {copyToClipboard} from "@/utils/clipboardUtils";
import {formatEntityBarcode} from "@/utils/barcodeUtils";
import {openPrintPage, type PrintItem} from "@/utils/printUtils";
import type {BarcodeType} from "@/pages/PrintPage/BarcodeLabel";
import MarketplaceAccountChip from "@/components/marketplace/MarketplaceAccountChip";
import {useBackClosable} from "@/hooks/useBackClosable.ts";
import {useRetainedValue} from "@/hooks/useRetainedValue";
import CatalogItemLink from "@/components/catalog/CatalogItemLink.tsx";
import {CATALOG_ITEM_TYPE_CONFIG} from "@/features/catalog";

const DRAWER_WIDTH = 1000;

// ─── Form types ───────────────────────────────────────────────────────────────

type ComponentValue = {
  entityId?: string;
  component: CatalogItemSelectDto | null;
  quantity: number;
};

type ImageValue = {
  /** Id of the join row, absent until the image is saved with the item. */
  entityId?: string;
  file: DataFileDto;
};

type ChildValue = {
  entityId?: string;
  type: "standard" | "unit";
  name: string;
  article: string;
  barcode: string;
  description: string;
  notes: string;
  labelText: string;
  tags: CatalogItemTagDto[];
  mainImage: DataFileDto | null;
  images: ImageValue[];
};

type CatalogItemFormValues = {
  name: string;
  article: string;
  barcode: string;
  description: string;
  notes: string;
  labelText: string;
  isArchived: boolean;
  tags: CatalogItemTagDto[];
  members: CatalogItemSelectDto[];
  components: ComponentValue[];
  children: ChildValue[];
  mainImage: DataFileDto | null;
  images: ImageValue[];
};

// ─── Helpers ─────────────────────────────────────────────────────────────────

function toSelectDto(dto: CatalogItemDto): CatalogItemSelectDto {
  return {
    id: dto.id,
    type: dto.type,
    name: dto.name,
    fullName: dto.fullName,
    article: dto.article,
    isArchived: dto.isArchived,
  };
}

function toPartialSelectDto(
  id: string,
  fullName: string,
  type: CatalogItemSelectDto["type"] = "standard",
): CatalogItemSelectDto {
  return {id, type, name: fullName, fullName, article: "", isArchived: false};
}

/** Order is the array index, so a drag-reorder needs no extra state. */
function mapImagesToRequest(images: ImageValue[]) {
  return images.map((image, index) => ({
    id: image.entityId ?? null,
    fileId: image.file.id,
    order: index,
  }));
}

function mapFormToRequest(values: CatalogItemFormValues): UpdateCatalogItemRequest {
  return {
    name: values.name,
    article: values.article,
    barcode: values.barcode || null,
    description: values.description || null,
    notes: values.notes || null,
    labelText: values.labelText || null,
    isArchived: values.isArchived,
    groupId: null,
    tags: values.tags.map((t) => t.id),
    memberIds: values.members.map((m) => m.id),
    components: values.components
      .filter((c) => c.component !== null)
      .map((c) => ({id: c.entityId ?? null, componentId: c.component!.id, quantity: c.quantity})),
    children: values.children.map((c) => ({
      id: c.entityId ?? null,
      type: c.type,
      name: c.name,
      article: c.article,
      barcode: c.barcode || null,
      description: c.description || null,
      notes: c.notes || null,
      labelText: c.labelText || null,
      isArchived: values.isArchived,
      tags: c.tags.map((t) => t.id),
      mainImageFileId: c.mainImage?.id ?? null,
      images: mapImagesToRequest(c.images),
    })),
    mainImageFileId: values.mainImage?.id ?? null,
    images: mapImagesToRequest(values.images),
  };
}

// ─── ImagesFields ─────────────────────────────────────────────────────────────

/**
 * Main image plus gallery. Used both for the item itself and for each product group child, so the
 * field names are passed in; the two casts are the price of one shared component.
 */
function ImagesFields({
  control,
  disabled,
  mainName,
  listName,
  compact,
}: {
  control: Control<CatalogItemFormValues>;
  disabled?: boolean;
  mainName: FieldPath<CatalogItemFormValues>;
  listName: FieldPath<CatalogItemFormValues>;
  compact?: boolean;
}) {
  return (
    <Stack direction={compact ? "column" : "row"} spacing={2} sx={{alignItems: "flex-start"}}>
      <Controller
        control={control}
        name={mainName}
        render={({field}) => (
          <SingleFileControl
            value={field.value as DataFileDto | null}
            onChange={field.onChange}
            View={ImageCardFileView}
            Input={AddFileInput}
            accept="image/*"
            disabled={disabled}
            inputLabel="Главное фото"
          />
        )}
      />
      <Controller
        control={control}
        name={listName}
        render={({field}) => {
          const images = (field.value ?? []) as ImageValue[];
          return (
            <FileListControl
              value={images.map((i) => i.file)}
              // keep entityId for files that were already saved, so the join row is updated, not recreated
              onChange={(files) =>
                field.onChange(
                  files.map((file) => images.find((i) => i.file.id === file.id) ?? {file}),
                )
              }
              View={ImagePreviewFileView}
              Input={AddFileInput}
              accept="image/*"
              disabled={disabled}
              sortable
              inputLabel="Добавить фото"
            />
          );
        }}
      />
    </Stack>
  );
}

// ─── TagsAutocomplete ─────────────────────────────────────────────────────────

function TagsAutocomplete({
  value,
  onChange,
  disabled,
}: {
  value: CatalogItemTagDto[];
  onChange: (v: CatalogItemTagDto[]) => void;
  disabled?: boolean;
}) {
  const [inputValue, setInputValue] = useState("");
  const debouncedInput = useDebounce(inputValue, 300);
  const tagsQuery = useQuery(catalogGetTagsOptions({query: {search: debouncedInput || undefined}}));
  const createMutation = useMutation(catalogCreateTagMutation());

  const options = useMemo(() => {
    const results = tagsQuery.data ?? [];
    const seen = new Set(results.map((t) => t.id));
    return [...results, ...value.filter((t) => !seen.has(t.id))];
  }, [tagsQuery.data, value]);

  const NEW_TAG_PREFIX = "__new__:";

  const handleChange = async (
    _: React.SyntheticEvent,
    newValue: (CatalogItemTagDto | string)[],
  ) => {
    const resolved: CatalogItemTagDto[] = [];
    for (const item of newValue) {
      if (typeof item === "string") {
        // freeSolo raw string — Enter pressed directly
        const trimmed = item.trim();
        if (!trimmed) continue;
        const created = await createMutation.mutateAsync({body: {name: trimmed}});
        resolved.push(created);
      } else if (item.id.startsWith(NEW_TAG_PREFIX)) {
        // "Create" option selected from dropdown
        const name = item.id.slice(NEW_TAG_PREFIX.length);
        const created = await createMutation.mutateAsync({body: {name}});
        resolved.push(created);
      } else {
        resolved.push(item);
      }
    }
    onChange(resolved);
  };

  return (
    <Autocomplete
      multiple
      freeSolo
      options={options}
      value={value}
      onChange={handleChange}
      inputValue={inputValue}
      onInputChange={(_, v) => setInputValue(v)}
      getOptionLabel={(t) => (typeof t === "string" ? t : (t as CatalogItemTagDto).name)}
      isOptionEqualToValue={(o, v) =>
        typeof o !== "string" && typeof v !== "string" && o.id === v.id
      }
      filterSelectedOptions
      filterOptions={(x, params) => {
        const trimmed = params.inputValue.trim();
        const alreadyExists = x.some(
          (o) => typeof o != "string" && o.name.toLowerCase() === trimmed.toLowerCase(),
        );
        if (trimmed && !alreadyExists) {
          return [
            ...x,
            {id: `${NEW_TAG_PREFIX}${trimmed}`, name: `Создать «${trimmed}»`} as CatalogItemTagDto,
          ];
        }
        return x;
      }}
      loading={tagsQuery.isLoading || createMutation.isPending}
      disabled={disabled || createMutation.isPending}
      size="small"
      renderInput={(params) => <TextField {...params} label="Теги" />}
      renderValue={(tagValue, getItemProps) =>
        tagValue.map((option, index) => {
          const tag = option as CatalogItemTagDto;
          return <Chip label={tag.name} {...getItemProps({index})} key={tag.id} size="small" />;
        })
      }
    />
  );
}

// ─── ViewMode ─────────────────────────────────────────────────────────────────

function LabeledRow({label, children}: {label: string; children: React.ReactNode}) {
  return (
    <Stack direction="row" spacing={1} sx={{alignItems: "baseline"}}>
      <Typography color="text.secondary" sx={{width: 140, flexShrink: 0}} variant="body2">
        {label}
      </Typography>
      <Box sx={{flex: 1}}>{children}</Box>
    </Stack>
  );
}

/** Read-only image strip. The inherited marker matters: without it there is no way to tell an
 *  item's own photo from the group's. */
function ItemImagesView({item}: {item: CatalogItemDto}) {
  const {showModal} = useModal();

  const gallery = [...(item.mainImage ? [item.mainImage] : []), ...item.images.map((i) => i.file)];
  if (gallery.length === 0) return null;

  const inherited = !!item.mainImage && !item.mainImageFileId;

  return (
    <Stack spacing={0.5}>
      <Stack direction="row" spacing={1} sx={{flexWrap: "wrap", gap: 1}}>
        {gallery.map((file, index) => (
          <Box
            key={file.id}
            onClick={() =>
              showModal(FileViewerModal, {files: gallery.map(viewable), initialIndex: index})
            }
            sx={{
              width: 96,
              height: 96,
              borderRadius: 1,
              overflow: "hidden",
              border: "1px solid",
              borderColor: "divider",
              cursor: "pointer",
            }}
          >
            <FileImage source={file} previewWidth={192} style={{height: "100%"}} />
          </Box>
        ))}
      </Stack>
      {inherited && (
        <Typography variant="caption" color="text.secondary">
          Главное фото унаследовано от группы
        </Typography>
      )}
    </Stack>
  );
}

function ViewMode({
  itemId,
  onEdit,
  onDelete,
  canEdit,
  onOpenItem,
  showLoadingOverlay,
}: {
  itemId: string;
  onEdit: () => void;
  onDelete: () => void;
  canEdit: boolean;
  onOpenItem?: (id: string) => void;
  showLoadingOverlay: boolean;
}) {
  const {data, isLoading, isError, isRefetchError, error} = useQuery({
    ...catalogGetByIdOptions({path: {id: itemId}}),
    meta: {suppressGlobalError: true, suppressGlobalNotFound: true},
  });

  const variationQueries = useQueries({
    queries: (data?.variationIds ?? []).map((id) => ({
      ...catalogGetByIdOptions({path: {id}}),
      meta: {suppressGlobalError: true},
    })),
  });
  const memberQueries = useQueries({
    queries: (data?.memberIds ?? []).map((id) => ({
      ...catalogGetByIdOptions({path: {id}}),
      meta: {suppressGlobalError: true},
    })),
  });

  if (isLoading) {
    return (
      <Box sx={{display: "flex", justifyContent: "center", pt: 4}}>
        <CircularProgress />
      </Box>
    );
  }
  if (isError && !isRefetchError)
    return isNotFoundError(error) ? <NotFound /> : <QueryError error={error} />;
  if (!data) return null;

  const isStandardOrUnit = data.type === "standard" || data.type === "unit";

  return (
    <Box sx={{position: "relative", display: "flex", flex: 1, minHeight: 0}}>
      <LoadingOverlay open={showLoadingOverlay} />
      <Box sx={{overflowY: "auto", px: 2, py: 2, flex: 1}}>
        <Stack spacing={2}>
          <ItemImagesView item={data} />

          {/* Basic fields */}
          <Stack spacing={1}>
            {data.isArchived && (
              <Chip label="В архиве" color="warning" size="small" sx={{alignSelf: "flex-start"}} />
            )}
            <LabeledRow label="Артикул">
              <Typography variant="body2">{data.article}</Typography>
            </LabeledRow>
            <LabeledRow label="Штрихкод">
              <Typography variant="body2">{data.barcode ?? "—"}</Typography>
            </LabeledRow>
            <LabeledRow label="Описание">
              <Typography variant="body2" sx={{whiteSpace: "pre-wrap"}}>
                {data.description ?? "—"}
              </Typography>
            </LabeledRow>
            <LabeledRow label="Заметки">
              <Typography variant="body2" sx={{whiteSpace: "pre-wrap"}}>
                {data.notes ?? "—"}
              </Typography>
            </LabeledRow>
            <LabeledRow label="Текст на этикетке">
              <Typography variant="body2">
                {data.labelText ?? `${data.effectiveLabelText}`}
              </Typography>
            </LabeledRow>
            {data.groupId && (
              <LabeledRow label="Группа">
                <Stack direction="row" spacing={0.5} sx={{alignItems: "center"}}>
                  <Typography variant="body2">{data.groupName}</Typography>
                  {onOpenItem && (
                    <Tooltip title="Открыть группу">
                      <IconButton
                        size="small"
                        onClick={() => onOpenItem(data.groupId!)}
                        sx={{p: 0.25}}
                      >
                        <OpenInNewIcon sx={{fontSize: 14}} />
                      </IconButton>
                    </Tooltip>
                  )}
                </Stack>
              </LabeledRow>
            )}
          </Stack>

          {/* Tags */}
          {data.tags.length > 0 && (
            <>
              <Divider />
              <Box sx={{display: "flex", flexWrap: "wrap", gap: 0.5}}>
                {data.tags.map((tag) => (
                  <Chip key={tag.id} label={tag.name} size="small" variant="outlined" />
                ))}
              </Box>
            </>
          )}

          {/* Marketplaces Accounts */}
          {data.marketplaceAccounts.length > 0 && (
            <>
              <Divider />
              <Stack spacing={1}>
                <Typography variant="subtitle2">Привзязан к карточкам</Typography>
                <Box sx={{display: "flex", flexWrap: "wrap", gap: 0.5}}>
                  {data.marketplaceAccounts.map((account) => (
                    <MarketplaceAccountChip
                      key={account.id}
                      accountId={account.id}
                      name={account.name}
                      type={account.type}
                      search={`?tab=cards&catalogItemId=${data.id}&mappingState=all`}
                    />
                  ))}
                </Box>
              </Stack>
            </>
          )}

          {/* Variations (Standard/Unit) */}
          {isStandardOrUnit && data.variationIds.length > 0 && (
            <>
              <Divider />
              <Stack spacing={1}>
                <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
                  <Typography variant="subtitle2">Состоит в вариациях</Typography>
                  <Chip label={data.variationIds.length} size="small" />
                </Stack>
                <Box sx={{display: "flex", flexWrap: "wrap", gap: 0.5}}>
                  {variationQueries.map((q, i) =>
                    q.data ? (
                      <Chip
                        key={q.data.id}
                        label={q.data.fullName}
                        size="small"
                        variant="outlined"
                        onClick={onOpenItem ? () => onOpenItem(q.data!.id) : undefined}
                        sx={{cursor: onOpenItem ? "pointer" : "default"}}
                      />
                    ) : (
                      <Chip key={i} label="…" size="small" variant="outlined" />
                    ),
                  )}
                </Box>
              </Stack>
            </>
          )}

          {/* Members (Variation) */}
          {data.type === "variation" && data.memberIds.length > 0 && (
            <>
              <Divider />
              <Stack spacing={1}>
                <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
                  <Typography variant="subtitle2">Участники</Typography>
                  <Chip label={data.memberIds.length} size="small" />
                </Stack>
                <Box sx={{display: "flex", flexWrap: "wrap", gap: 0.5}}>
                  {memberQueries.map((q, i) =>
                    q.data ? (
                      <Chip
                        key={q.data.id}
                        label={q.data.fullName}
                        size="small"
                        variant="filled"
                        onClick={onOpenItem ? () => onOpenItem(q.data!.id) : undefined}
                        sx={{cursor: onOpenItem ? "pointer" : "default"}}
                        color={CATALOG_ITEM_TYPE_CONFIG[q.data.type].color}
                      />
                    ) : (
                      <Chip key={i} label="…" size="small" variant="outlined" />
                    ),
                  )}
                </Box>
              </Stack>
            </>
          )}

          {/* Components (Bundle) */}
          {data.type === "bundle" && data.components.length > 0 && (
            <>
              <Divider />
              <Stack spacing={1}>
                <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
                  <Typography variant="subtitle2">Компоненты</Typography>
                  <Chip label={data.components.length} size="small" />
                </Stack>
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>Тип</TableCell>
                      <TableCell>Позиция</TableCell>
                      <TableCell align="right">Кол-во</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {data.components.map((c) => (
                      <TableRow key={c.id}>
                        <TableCell>
                          <CatalogItemTypeChip type={c.componentType} />
                        </TableCell>
                        <TableCell>
                          {onOpenItem ? (
                            <CatalogItemLink
                              catalogItemId={c.componentId}
                              onOpen={onOpenItem}
                              sx={{gap: 1}}
                            >
                              {c.componentName}
                            </CatalogItemLink>
                          ) : (
                            c.componentName
                          )}
                        </TableCell>
                        <TableCell align="right">{c.quantity}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </Stack>
            </>
          )}

          {/* Children (ProductGroup) */}
          {data.type === "productGroup" && data.children.length > 0 && (
            <>
              <Divider />
              <Stack spacing={1}>
                <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
                  <Typography variant="subtitle2">Позиции группы</Typography>
                  <Chip label={data.children.length} size="small" />
                </Stack>
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>Название</TableCell>
                      <TableCell>Артикул</TableCell>
                      <TableCell>Тип</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {data.children.map((child) => (
                      <TableRow
                        key={child.id}
                        hover
                        sx={{cursor: onOpenItem ? "pointer" : "default"}}
                        onClick={() => onOpenItem?.(child.id)}
                      >
                        <TableCell>{child.name}</TableCell>
                        <TableCell>{child.article}</TableCell>
                        <TableCell>
                          <CatalogItemTypeChip type={child.type} />
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </Stack>
            </>
          )}

          {/* Actions */}
          {canEdit && !data.groupId && (
            <Stack direction="row" spacing={1} sx={{pt: 1}}>
              <Button size="small" startIcon={<EditIcon />} onClick={onEdit}>
                Редактировать
              </Button>
              <Button size="small" color="error" startIcon={<DeleteIcon />} onClick={onDelete}>
                Удалить
              </Button>
            </Stack>
          )}
        </Stack>
      </Box>
    </Box>
  );
}

// ─── BundleComponentRow ───────────────────────────────────────────────────────

function BundleComponentRow({
  control,
  setValue,
  index,
  onRemove,
  isPending,
}: {
  control: Control<CatalogItemFormValues>;
  setValue: UseFormSetValue<CatalogItemFormValues>;
  index: number;
  onRemove: () => void;
  isPending: boolean;
}) {
  const component = useWatch({control, name: `components.${index}.component`});
  return (
    <Stack direction="row" spacing={1} sx={{alignItems: "flex-start"}}>
      <Box sx={{flex: 1}}>
        <CatalogItemsSelect
          value={component?.id ?? null}
          onChange={(id) => {
            if (!id) setValue(`components.${index}.component`, null);
          }}
          onDtoChange={(dto) => setValue(`components.${index}.component`, dto)}
          types={["standard", "unit", "productGroup", "variation"]}
          label="Позиция"
          disabled={isPending}
          size="small"
          textFieldProps={{size: "small"}}
        />
      </Box>
      <Controller
        control={control}
        name={`components.${index}.quantity`}
        rules={{required: true, min: {value: 1, message: "Мин. 1"}}}
        render={({field: f, fieldState}) => (
          <TextField
            {...f}
            label="Кол-во"
            type="number"
            size="small"
            sx={{width: 90}}
            disabled={isPending}
            error={!!fieldState.error}
            helperText={fieldState.error?.message}
            slotProps={{htmlInput: {min: 1}}}
          />
        )}
      />
      <IconButton size="small" onClick={onRemove} disabled={isPending} sx={{mt: 0.5}}>
        <DeleteIcon fontSize="small" />
      </IconButton>
    </Stack>
  );
}

// ─── ChildRow ─────────────────────────────────────────────────────────────────

function ChildRow({
  control,
  setValue,
  index,
  onRemove,
  isPending,
}: {
  control: Control<CatalogItemFormValues>;
  setValue: UseFormSetValue<CatalogItemFormValues>;
  index: number;
  onRemove: () => void;
  isPending: boolean;
}) {
  const childTags = useWatch({control, name: `children.${index}.tags`});
  const entityId = useWatch({control, name: `children.${index}.entityId`});
  return (
    <Box sx={{border: 1, borderColor: "divider", borderRadius: 1, p: 1.5}}>
      <Stack spacing={1.5}>
        <Stack direction="row" sx={{justifyContent: "space-between", alignItems: "center"}}>
          <Controller
            control={control}
            name={`children.${index}.type`}
            render={({field: f}) => (
              <Select {...f} size="small" disabled={isPending || !!entityId} sx={{minWidth: 120}}>
                <MenuItem value="standard">Товар</MenuItem>
                <MenuItem value="unit">Штучный</MenuItem>
              </Select>
            )}
          />
          <IconButton size="small" color="error" onClick={onRemove} disabled={isPending}>
            <DeleteIcon fontSize="small" />
          </IconButton>
        </Stack>
        <FormTextField
          control={control}
          name={`children.${index}.name`}
          label="Название"
          size="small"
          fullWidth
          disabled={isPending}
          rules={{required: "Обязательное поле"}}
        />
        <FormTextField
          control={control}
          name={`children.${index}.article`}
          label="Артикул"
          size="small"
          fullWidth
          disabled={isPending}
          rules={{required: "Обязательное поле"}}
        />
        <FormTextField
          control={control}
          name={`children.${index}.barcode`}
          label="Штрихкод"
          size="small"
          fullWidth
          disabled={isPending}
        />
        <FormTextField
          control={control}
          name={`children.${index}.description`}
          label="Описание"
          size="small"
          fullWidth
          disabled={isPending}
        />
        <FormTextField
          control={control}
          name={`children.${index}.notes`}
          label="Заметки"
          size="small"
          fullWidth
          disabled={isPending}
        />
        <FormTextField
          control={control}
          name={`children.${index}.labelText`}
          label="Текст на этикетке"
          size="small"
          fullWidth
          disabled={isPending}
          helperText="По умолчанию — артикул"
        />
        <TagsAutocomplete
          value={childTags}
          onChange={(v) => setValue(`children.${index}.tags`, v)}
          disabled={isPending}
        />
        <ImagesFields
          control={control}
          disabled={isPending}
          mainName={`children.${index}.mainImage`}
          listName={`children.${index}.images`}
        />
      </Stack>
    </Box>
  );
}

// ─── EditMode ─────────────────────────────────────────────────────────────────

function EditMode({itemId, onClose}: {itemId: string; onClose: () => void}) {
  const queryClient = useQueryClient();

  const {data, isLoading: isItemLoading} = useQuery({
    ...catalogGetByIdOptions({path: {id: itemId}}),
    meta: {suppressGlobalError: true, suppressGlobalNotFound: true},
  });

  const memberQueries = useQueries({
    queries: (data?.memberIds ?? []).map((id) => ({
      ...catalogGetByIdOptions({path: {id}}),
      meta: {suppressGlobalError: true},
    })),
  });

  const membersResolved = memberQueries.every((q) => !q.isPending);

  const form = useForm<CatalogItemFormValues>({
    defaultValues: {
      name: "",
      article: "",
      barcode: "",
      description: "",
      notes: "",
      labelText: "",
      isArchived: false,
      tags: [],
      members: [],
      components: [],
      children: [],
      mainImage: null,
      images: [],
    },
  });
  const {setApiError} = useRhfApiErrors(form);
  const {reset, setValue, control, formState} = form;

  useEffect(() => {
    if (!data || !membersResolved) return;
    const memberSummaries = memberQueries.filter((q) => q.data).map((q) => toSelectDto(q.data!));
    reset({
      name: data.name,
      article: data.article,
      barcode: data.barcode ?? "",
      description: data.description ?? "",
      notes: data.notes ?? "",
      labelText: data.labelText ?? "",
      isArchived: data.isArchived,
      tags: [...data.tags],
      members: memberSummaries,
      components: data.components.map((c) => ({
        entityId: c.id,
        component: toPartialSelectDto(c.componentId, c.componentName, c.componentType),
        quantity: c.quantity,
      })),
      children: data.children.map((c) => ({
        entityId: c.id,
        type: c.type as "standard" | "unit",
        name: c.name,
        article: c.article,
        barcode: c.barcode ?? "",
        description: c.description ?? "",
        notes: c.notes ?? "",
        labelText: c.labelText ?? "",
        tags: [...c.tags],
        // a child's own image only — an inherited one must not be saved back as its own
        mainImage: c.mainImageFileId ? (c.mainImage ?? null) : null,
        images: c.images.map((i) => ({entityId: i.id, file: i.file})),
      })),
      mainImage: data.mainImageFileId ? (data.mainImage ?? null) : null,
      images: data.images.map((i) => ({entityId: i.id, file: i.file})),
    });
    // memberQueries changes reference each render — intentionally excluded
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [data?.id, membersResolved]);

  const mutation = useMutation({
    ...catalogUpdateMutation(),
    meta: {suppressGlobalError: true},
    onSuccess: async () => {
      await queryClient.invalidateQueries({queryKey: catalogGetByIdQueryKey({path: {id: itemId}})});
      await queryClient.invalidateQueries({queryKey: catalogGetAllQueryKey()});
      onClose();
    },
    onError: setApiError,
  });

  const isPending = mutation.isPending;
  const type = data?.type;

  const componentsArray = useFieldArray({control, name: "components"});
  const childrenArray = useFieldArray({control, name: "children"});

  const tags = useWatch({control, name: "tags"});
  const members = useWatch({control, name: "members"});

  const onSubmit = form.handleSubmit((values) => {
    mutation.mutate({path: {id: itemId}, body: mapFormToRequest(values)});
  });

  if (isItemLoading)
    return (
      <Box sx={{display: "flex", justifyContent: "center", pt: 4}}>
        <CircularProgress />
      </Box>
    );
  if (!data) return null;

  if (data.groupId) {
    return (
      <Box sx={{px: 2, py: 2}}>
        <Alert severity="info">
          Эта позиция управляется группой «{data.groupName}». Редактируйте через группу.
        </Alert>
      </Box>
    );
  }

  return (
    <Box
      component="form"
      onSubmit={onSubmit}
      sx={{overflowY: "auto", px: 2, py: 2, flex: 1, display: "flex", flexDirection: "column"}}
    >
      <Stack spacing={2} sx={{flex: 1}}>
        {/* Base fields */}
        <FormTextField
          control={control}
          name="name"
          label="Название"
          size="small"
          fullWidth
          disabled={isPending}
          rules={{required: "Обязательное поле"}}
        />
        <FormTextField
          control={control}
          name="article"
          label="Артикул"
          size="small"
          fullWidth
          disabled={isPending}
          rules={{required: "Обязательное поле"}}
        />
        <FormTextField
          control={control}
          name="barcode"
          label="Штрихкод"
          size="small"
          fullWidth
          disabled={isPending}
        />
        <FormTextField
          control={control}
          name="description"
          label="Описание"
          size="small"
          fullWidth
          multiline
          minRows={2}
          disabled={isPending}
        />
        <FormTextField
          control={control}
          name="notes"
          label="Заметки"
          size="small"
          fullWidth
          multiline
          minRows={2}
          disabled={isPending}
        />
        <FormTextField
          control={control}
          name="labelText"
          label="Текст на этикетке"
          size="small"
          fullWidth
          disabled={isPending}
          helperText="По умолчанию — артикул"
        />
        <Divider textAlign="left">
          <Typography variant="caption" color="text.secondary">
            Изображения
          </Typography>
        </Divider>
        <ImagesFields
          control={control}
          disabled={isPending}
          mainName="mainImage"
          listName="images"
        />
        <Controller
          control={control}
          name="isArchived"
          render={({field}) => (
            <FormControlLabel
              control={
                <Switch
                  checked={field.value}
                  onChange={(e) => field.onChange(e.target.checked)}
                  disabled={isPending}
                />
              }
              label="В архиве"
            />
          )}
        />
        <TagsAutocomplete value={tags} onChange={(v) => setValue("tags", v)} disabled={isPending} />

        {/* Variation — Members */}
        {type === "variation" && (
          <>
            <Divider />
            <Typography variant="subtitle2">Участники</Typography>
            <CatalogItemsSelect
              multiple
              value={members}
              onChange={(v) => setValue("members", v)}
              types={["standard", "unit", "bundle"]}
              label="Участники"
              disabled={isPending}
              size="small"
            />
          </>
        )}

        {/* Bundle — Components */}
        {type === "bundle" && (
          <>
            <Divider />
            <Stack direction="row" sx={{justifyContent: "space-between", alignItems: "center"}}>
              <Typography variant="subtitle2">Компоненты</Typography>
              <Button
                size="small"
                startIcon={<AddIcon />}
                onClick={() => componentsArray.append({component: null, quantity: 1})}
                disabled={isPending}
              >
                Добавить
              </Button>
            </Stack>
            {componentsArray.fields.map((field, index) => (
              <BundleComponentRow
                key={field.id}
                control={control}
                setValue={setValue}
                index={index}
                onRemove={() => componentsArray.remove(index)}
                isPending={isPending}
              />
            ))}
          </>
        )}

        {/* ProductGroup — Children */}
        {type === "productGroup" && (
          <>
            <Divider />
            <Stack direction="row" sx={{justifyContent: "space-between", alignItems: "center"}}>
              <Typography variant="subtitle2">Позиции группы</Typography>
              <Button
                size="small"
                startIcon={<AddIcon />}
                onClick={() =>
                  childrenArray.append({
                    type: "standard",
                    name: "",
                    article: "",
                    barcode: "",
                    description: "",
                    notes: "",
                    labelText: "",
                    tags: [],
                    mainImage: null,
                    images: [],
                  })
                }
                disabled={isPending}
              >
                Добавить
              </Button>
            </Stack>
            {childrenArray.fields.map((field, index) => (
              <ChildRow
                key={field.id}
                control={control}
                setValue={setValue}
                index={index}
                onRemove={() => childrenArray.remove(index)}
                isPending={isPending}
              />
            ))}
          </>
        )}

        {formState.errors.root && <Alert severity="error">{formState.errors.root.message}</Alert>}
      </Stack>

      <Stack direction="row" spacing={1} sx={{justifyContent: "flex-end", pt: 2, flexShrink: 0}}>
        <Button onClick={onClose} disabled={isPending}>
          Отмена
        </Button>
        <Button type="submit" variant="contained" disabled={isPending}>
          {isPending ? <CircularProgress size={20} color="inherit" /> : "Сохранить"}
        </Button>
      </Stack>
    </Box>
  );
}

// ─── PrintLabelDialog ─────────────────────────────────────────────────────────

type LabelKind = "internal" | "barcode";

/** bwip-js rejects EAN13 payloads that are not 12–13 digits, so anything else prints as Code128. */
function barcodeTypeFor(barcode: string): BarcodeType {
  return /^\d{12,13}$/.test(barcode) ? "EAN13" : "Code128";
}

function PrintLabelDialog({
  item,
  open,
  onClose,
}: {
  item: CatalogItemDto;
  open: boolean;
  onClose: () => void;
}) {
  const [kind, setKind] = useState<LabelKind>("internal");
  const [copies, setCopies] = useState(1);
  const [prevOpen, setPrevOpen] = useState(open);

  if (prevOpen !== open) {
    setPrevOpen(open);
    if (open) {
      setKind("internal");
      setCopies(1);
    }
  }

  const label = item.article ? `${item.fullName} · ${item.article}` : item.fullName;

  const handlePrint = () => {
    const printItem: PrintItem =
      kind === "internal"
        ? {type: "DataMatrix", value: formatEntityBarcode("catalogItem", item.id), label}
        : {type: barcodeTypeFor(item.barcode!), value: item.barcode!, label};
    openPrintPage(Array.from({length: copies}, () => printItem));
    onClose();
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="xs" fullWidth>
      <DialogTitle>Печать этикетки</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{pt: 1}}>
          <RadioGroup value={kind} onChange={(e) => setKind(e.target.value as LabelKind)}>
            <FormControlLabel
              value="internal"
              control={<Radio size="small" />}
              label="Внутренний код (DataMatrix)"
            />
            <FormControlLabel
              value="barcode"
              control={<Radio size="small" />}
              disabled={!item.barcode}
              label={
                item.barcode ? `Штрихкод товара — ${item.barcode}` : "Штрихкод товара — не заполнен"
              }
            />
          </RadioGroup>
          <TextField
            label="Количество копий"
            type="number"
            size="small"
            value={copies}
            onChange={(e) => setCopies(Math.max(1, Math.min(200, Number(e.target.value) || 1)))}
            slotProps={{htmlInput: {min: 1, max: 200}}}
          />
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Отмена</Button>
        <Button variant="contained" startIcon={<PrintIcon />} onClick={handlePrint}>
          Печать
        </Button>
      </DialogActions>
    </Dialog>
  );
}

// ─── CatalogItemDrawer ────────────────────────────────────────────────────────

export interface CatalogItemDrawerProps {
  itemId: string | null;
  onClose: () => void;
  onOpenItem?: (id: string) => void;
  backClosable?: boolean;
}

export function CatalogItemDrawer({
  itemId,
  onClose,
  onOpenItem,
  backClosable,
}: CatalogItemDrawerProps) {
  const [isEditing, setIsEditing] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [printOpen, setPrintOpen] = useState(false);
  const canEdit = useHasPermission("catalog.edit");
  const queryClient = useQueryClient();
  const {enqueueSnackbar} = useSnackbar();

  // Content keeps rendering the closing item until the exit animation ends.
  const [shownItemId, releaseShownItem] = useRetainedValue(itemId);
  const [prevItemId, setPrevItemId] = useState(shownItemId);

  useBackClosable(!!itemId && !!backClosable, onClose);

  if (prevItemId !== shownItemId) {
    setPrevItemId(shownItemId);
    setIsEditing(false);
    setDeleteOpen(false);
    setPrintOpen(false);
  }

  const {data, dataUpdatedAt, isFetching, isLoading} = useQuery({
    ...catalogGetByIdOptions({path: {id: shownItemId!}}),
    enabled: !!shownItemId,
    meta: {suppressGlobalError: true, suppressGlobalNotFound: true},
  });

  const refreshItem = useCallback(() => {
    void queryClient.invalidateQueries({
      queryKey: byOperation("catalogGetById", {path: {id: shownItemId!}}),
    });
  }, [queryClient, shownItemId]);

  // The drawer opens read-only, so the lock is taken only once the user actually starts editing.
  const lock = useEditLock("catalogItem", shownItemId, {
    isDirty: isEditing,
    dataUpdatedAt,
    isFetching,
    isLoading,
    onRefresh: refreshItem,
    enabled: isEditing && canEdit && !!itemId,
  });

  const deleteMutation = useMutation({
    ...catalogDeleteMutation(),
    onSuccess: async () => {
      await queryClient.invalidateQueries({queryKey: catalogGetAllQueryKey()});
      onClose();
      setDeleteOpen(false);
    },
  });

  const handleClose = () => {
    setDeleteOpen(false);
    setPrintOpen(false);
    onClose();
  };

  const handleCopyId = async () => {
    if (!data) return;
    const copied = await copyToClipboard(data.id);
    enqueueSnackbar(copied ? "GUID скопирован" : "Не удалось скопировать GUID", {
      variant: copied ? "success" : "error",
    });
  };

  const handleOpenItem = onOpenItem
    ? (id: string) => {
        setIsEditing(false);
        setDeleteOpen(false);
        onOpenItem(id);
      }
    : undefined;

  return (
    <Drawer
      anchor="right"
      open={!!itemId}
      onClose={handleClose}
      slotProps={{
        transition: {onExited: releaseShownItem},
        paper: {
          sx: {
            width: DRAWER_WIDTH,
            maxWidth: "calc(100vw - 10px)",
            display: "flex",
            flexDirection: "column",
          },
        },
      }}
    >
      <Stack
        direction="row"
        sx={{
          alignItems: "center",
          justifyContent: "space-between",
          px: 2,
          py: 1.5,
          flexShrink: 0,
          gap: 1,
        }}
      >
        <Stack direction="row" spacing={1} sx={{alignItems: "center", flex: 1, minWidth: 0}}>
          {data?.type && <CatalogItemTypeChip type={data.type} />}
          <Typography variant="h6" noWrap sx={{flex: 1}}>
            {data?.fullName ?? ""}
          </Typography>
          {isEditing && <EntityViewers entityType="catalogItem" entityId={shownItemId} />}
        </Stack>
        <Stack direction="row" spacing={0.5} sx={{alignItems: "center"}}>
          <Tooltip title="Скопировать GUID">
            <span>
              <IconButton size="small" disabled={!data} onClick={handleCopyId}>
                <ContentCopyIcon fontSize="small" />
              </IconButton>
            </span>
          </Tooltip>
          <Tooltip title="Печать этикетки">
            <span>
              <IconButton size="small" disabled={!data} onClick={() => setPrintOpen(true)}>
                <PrintIcon fontSize="small" />
              </IconButton>
            </span>
          </Tooltip>
          <IconButton onClick={handleClose} size="small">
            <CloseIcon />
          </IconButton>
        </Stack>
      </Stack>
      <Divider />

      <Box sx={{px: 2, "&:empty": {display: "none"}}}>
        <EditLockBanner heldBy={lock.heldBy} />
        <StaleDataBanner
          isStale={!lock.heldBy && lock.isStale}
          staleBy={lock.staleBy}
          onRefresh={lock.refresh}
          onDismiss={lock.dismissStale}
        />
      </Box>

      {shownItemId && !isEditing && (
        <ViewMode
          itemId={shownItemId}
          canEdit={canEdit}
          onEdit={() => setIsEditing(true)}
          onDelete={() => setDeleteOpen(true)}
          onOpenItem={handleOpenItem}
          showLoadingOverlay={lock.showLoadingOverlay}
        />
      )}
      {shownItemId && isEditing && (
        <EditMode itemId={shownItemId} onClose={() => setIsEditing(false)} />
      )}

      <ConfirmDialog
        open={deleteOpen}
        onClose={() => setDeleteOpen(false)}
        title="Удалить позицию?"
        onConfirm={() => deleteMutation.mutate({path: {id: shownItemId!}})}
        isPending={deleteMutation.isPending}
        confirmText="Удалить"
        confirmColor="error"
      >
        <Typography>Позиция «{data?.fullName}» будет удалена безвозвратно.</Typography>
      </ConfirmDialog>

      {data && (
        <PrintLabelDialog item={data} open={printOpen} onClose={() => setPrintOpen(false)} />
      )}
    </Drawer>
  );
}
