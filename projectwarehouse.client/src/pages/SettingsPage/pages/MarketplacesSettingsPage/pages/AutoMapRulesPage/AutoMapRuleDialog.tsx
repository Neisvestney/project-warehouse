import {useEffect} from "react";
import {
  Alert,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControl,
  FormControlLabel,
  InputLabel,
  MenuItem,
  Select,
  Stack,
  Switch,
} from "@mui/material";
import {useBackClosable} from "@/hooks/useBackClosable";
import {Controller, useForm} from "react-hook-form";
import {useMutation} from "@tanstack/react-query";
import {
  marketplaceAutoMapRulesCreateRuleMutation,
  marketplaceAutoMapRulesUpdateRuleMutation,
} from "@/api/@tanstack/react-query.gen";
import {useRhfApiErrors} from "@/hooks/useRhfApiErrors";
import CatalogItemsSelect from "@/components/CatalogItemsSelect";
import {FormTextField} from "@/components/form/FormTextField";
import {
  ALL_CARD_FIELDS,
  ALL_RULE_OPERATORS,
  CARD_FIELD_LABELS,
  RULE_OPERATOR_LABELS,
} from "../../marketplaceUtils";
import type {
  CatalogItemType,
  MarketplaceAutoMapRuleDto,
  MarketplaceCardField,
  MarketplaceRuleOperator,
} from "@/api/types.gen";

const MAPPABLE_TYPES: CatalogItemType[] = ["standard", "unit", "bundle", "variation"];

interface Values {
  field: MarketplaceCardField;
  operator: MarketplaceRuleOperator;
  value: string;
  catalogItemId: string | null;
  isEnabled: boolean;
  priority: string;
}

const EMPTY: Values = {
  field: "offerId",
  operator: "equals",
  value: "",
  catalogItemId: null,
  isEnabled: true,
  priority: "0",
};

function toValues(rule: MarketplaceAutoMapRuleDto): Values {
  return {
    field: rule.field,
    operator: rule.operator,
    value: rule.value,
    catalogItemId: rule.catalogItemId,
    isEnabled: rule.isEnabled,
    priority: String(rule.priority),
  };
}

interface AutoMapRuleDialogProps {
  open: boolean;
  /** null means create. */
  rule: MarketplaceAutoMapRuleDto | null;
  onClose: () => void;
  onSaved: () => Promise<void> | void;
}

function AutoMapRuleDialog({open, rule, onClose, onSaved}: AutoMapRuleDialogProps) {
  const form = useForm<Values>({defaultValues: EMPTY});
  const {control, handleSubmit, reset, formState} = form;
  const {setApiError} = useRhfApiErrors(form);

  useEffect(() => {
    if (open) reset(rule ? toValues(rule) : EMPTY);
  }, [open, rule, reset]);

  const onSuccess = async () => {
    await onSaved();
    onClose();
  };

  const create = useMutation({
    ...marketplaceAutoMapRulesCreateRuleMutation(),
    meta: {suppressGlobalError: true},
    onSuccess,
    onError: setApiError,
  });

  const update = useMutation({
    ...marketplaceAutoMapRulesUpdateRuleMutation(),
    meta: {suppressGlobalError: true},
    onSuccess,
    onError: setApiError,
  });

  const isPending = create.isPending || update.isPending;

  const submit = handleSubmit((values) => {
    if (!values.catalogItemId) return;

    const body = {
      field: values.field,
      operator: values.operator,
      value: values.value,
      catalogItemId: values.catalogItemId,
      isEnabled: values.isEnabled,
      priority: Number(values.priority),
    };

    if (rule) update.mutate({path: {id: rule.id}, body});
    else create.mutate({body});
  });

  useBackClosable(open && !isPending, onClose);

  return (
    <Dialog open={open} onClose={isPending ? undefined : onClose} maxWidth="sm" fullWidth>
      <DialogTitle>{rule ? "Правило автосопоставления" : "Новое правило"}</DialogTitle>
      <DialogContent>
        <Stack spacing={2} sx={{mt: 1}}>
          {formState.errors.root && <Alert severity="error">{formState.errors.root.message}</Alert>}
          <Stack direction="row" spacing={2}>
            <Controller
              control={control}
              name="field"
              render={({field}) => (
                <FormControl size="small" fullWidth>
                  <InputLabel>Поле карточки</InputLabel>
                  <Select {...field} label="Поле карточки">
                    {ALL_CARD_FIELDS.map((f) => (
                      <MenuItem key={f} value={f}>
                        {CARD_FIELD_LABELS[f]}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
              )}
            />
            <Controller
              control={control}
              name="operator"
              render={({field}) => (
                <FormControl size="small" fullWidth>
                  <InputLabel>Условие</InputLabel>
                  <Select {...field} label="Условие">
                    {ALL_RULE_OPERATORS.map((op) => (
                      <MenuItem key={op} value={op}>
                        {RULE_OPERATOR_LABELS[op]}
                      </MenuItem>
                    ))}
                  </Select>
                </FormControl>
              )}
            />
          </Stack>
          <FormTextField
            control={control}
            name="value"
            label="Значение"
            size="small"
            fullWidth
            rules={{required: "Укажите значение"}}
          />
          <Controller
            control={control}
            name="catalogItemId"
            rules={{required: "Выберите товар"}}
            render={({field, fieldState}) => (
              <CatalogItemsSelect
                label="Товар каталога"
                value={field.value}
                onChange={field.onChange}
                types={MAPPABLE_TYPES}
                size="small"
                textFieldProps={{
                  error: !!fieldState.error,
                  helperText: fieldState.error?.message,
                }}
              />
            )}
          />
          <Stack direction="row" spacing={2} sx={{alignItems: "center"}}>
            <FormTextField
              control={control}
              name="priority"
              label="Приоритет"
              type="number"
              size="small"
              helperText="Больше — применяется раньше"
              sx={{width: 200}}
            />
            <Controller
              control={control}
              name="isEnabled"
              render={({field}) => (
                <FormControlLabel
                  control={<Switch checked={field.value} onChange={field.onChange} />}
                  label="Активно"
                />
              )}
            />
          </Stack>
        </Stack>
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={isPending}>
          Отмена
        </Button>
        <Button variant="contained" onClick={submit} disabled={isPending}>
          {isPending ? <CircularProgress size={20} color="inherit" /> : "Сохранить"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

export default AutoMapRuleDialog;
