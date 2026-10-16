import type {TextFieldProps} from "@mui/material";
import {Autocomplete, Box, TextField, Typography} from "@mui/material";
import {useQuery} from "@tanstack/react-query";
import {organizationsGetShortOptions} from "@/api/@tanstack/react-query.gen";
import type {OrganizationShortSummaryDto} from "@/api/types.gen";

interface OrganizationsSelectProps {
  value: string | null;
  onChange: (id: string | null) => void;
  label?: string;
  disabled?: boolean;
  textFieldProps?: Partial<TextFieldProps>;
}

/** Loads the whole short list once: organizations are a handful of rows, so filtering stays local. */
function OrganizationsSelect({
  value,
  onChange,
  label = "Организация",
  disabled,
  textFieldProps,
}: OrganizationsSelectProps) {
  const {data, isLoading} = useQuery(organizationsGetShortOptions());
  const options = data ?? [];
  const selected = options.find((o) => o.id === value) ?? null;

  return (
    <Autocomplete<OrganizationShortSummaryDto>
      options={options}
      value={selected}
      onChange={(_, dto) => onChange(dto?.id ?? null)}
      getOptionLabel={(o) => `${o.name} · ${o.inn}`}
      isOptionEqualToValue={(o, v) => o.id === v.id}
      loading={isLoading}
      disabled={disabled}
      renderOption={({key, ...props}, o) => (
        <Box component="li" key={key} {...props}>
          <OrganizationOption name={o.name} inn={o.inn} />
        </Box>
      )}
      renderInput={(params) => <TextField {...params} label={label} {...textFieldProps} />}
    />
  );
}

function OrganizationOption({name, inn}: {name: string; inn: string}) {
  return (
    <Box>
      <Typography variant="body2">{name}</Typography>
      <Typography variant="caption" color="text.secondary" sx={{fontFamily: "monospace"}}>
        ИНН {inn}
      </Typography>
    </Box>
  );
}

export default OrganizationsSelect;
