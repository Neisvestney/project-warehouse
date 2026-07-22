import React, {useMemo, useRef, useState} from "react";
import {
  CircularProgress,
  Dialog,
  DialogContent,
  Divider,
  InputAdornment,
  List,
  ListItem,
  ListItemButton,
  ListItemIcon,
  ListItemText,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import SearchOffIcon from "@mui/icons-material/SearchOff";
import {useQuery} from "@tanstack/react-query";
import {useNavigate} from "react-router";
import {commonContentGlobalSearchOptions} from "@/api/@tanstack/react-query.gen";
import {resolveEntity} from "@/utils/appEntityUtils";
import {useDebounce} from "@/hooks/useDebounce";
import type {AppEntity} from "@/api";

type ResolvedEntity = ReturnType<typeof resolveEntity>;

interface GlobalSearchModalProps {
  open: boolean;
  onClose: () => void;
}

function GlobalSearchContent({
  onClose,
  inputRef,
}: {
  onClose: () => void;
  inputRef: React.RefObject<HTMLInputElement | null>;
}) {
  const [inputValue, setInputValue] = useState("");
  const [activeIndex, setActiveIndex] = useState(-1);
  const debouncedSearch = useDebounce(inputValue, 300);
  const navigate = useNavigate();
  const itemRefs = useRef<(HTMLLIElement | null)[]>([]);

  const searchQuery = useQuery({
    ...commonContentGlobalSearchOptions({query: {searchString: debouncedSearch || undefined}}),
    enabled: debouncedSearch.trim().length > 0,
  });

  const resolvedResults = useMemo(
    () =>
      ((searchQuery.data ?? []) as AppEntity[])
        .map(resolveEntity)
        .filter((e) => e.link !== "no-link" && e.link !== "#"),
    [searchQuery.data],
  );

  const handleSelect = (entity: ResolvedEntity) => {
    navigate(entity.link);
    onClose();
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    const count = resolvedResults.length;
    if (count === 0) return;

    if (e.key === "ArrowDown") {
      e.preventDefault();
      setActiveIndex(() => {
        const next = effectiveActiveIndex < count - 1 ? effectiveActiveIndex + 1 : 0;
        itemRefs.current[next]?.scrollIntoView({block: "nearest"});
        return next;
      });
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      setActiveIndex(() => {
        const next = effectiveActiveIndex > 0 ? effectiveActiveIndex - 1 : count - 1;
        itemRefs.current[next]?.scrollIntoView({block: "nearest"});
        return next;
      });
    } else if (e.key === "Enter") {
      if (resolvedResults[effectiveActiveIndex]) {
        handleSelect(resolvedResults[effectiveActiveIndex]);
      }
    }
  };

  const effectiveActiveIndex = activeIndex === -1 && resolvedResults.length > 0 ? 0 : activeIndex;

  const showList = debouncedSearch.trim().length > 0;
  const showEmpty = showList && !searchQuery.isFetching && resolvedResults.length === 0;

  return (
    <DialogContent sx={{p: 0}}>
      <TextField
        inputRef={inputRef}
        fullWidth
        placeholder="Поиск..."
        value={inputValue}
        onChange={(e) => {
          setInputValue(e.target.value);
          setActiveIndex(-1);
        }}
        onKeyDown={handleKeyDown}
        variant="outlined"
        slotProps={{
          input: {
            startAdornment: (
              <InputAdornment position="start">
                <Stack sx={{width: 24, height: 24, alignItems: "center", justifyContent: "center"}}>
                  {searchQuery.isFetching ? <CircularProgress size={20} /> : <SearchIcon />}
                </Stack>
              </InputAdornment>
            ),
          },
        }}
        sx={{
          "& .MuiOutlinedInput-root": {
            borderRadius: showList ? "8px 8px 0 0" : 2,
            "& fieldset": {border: "none"},
          },
        }}
      />
      {showList && (
        <>
          <Divider />
          <List dense sx={{maxHeight: 400, overflowY: "auto", py: 0}}>
            {showEmpty && (
              <ListItem sx={{justifyContent: "center", py: 3, gap: 1}}>
                <SearchOffIcon color="disabled" />
                <Typography color="text.secondary" variant="body1">
                  Ничего не найдено
                </Typography>
              </ListItem>
            )}
            {resolvedResults.map((entity, index) => (
              <ListItemButton
                key={entity.id ?? index}
                ref={(el) => {
                  itemRefs.current[index] = el as unknown as HTMLLIElement | null;
                }}
                selected={index === effectiveActiveIndex}
                onClick={() => handleSelect(entity)}
                onMouseEnter={() => setActiveIndex(index)}
              >
                <ListItemIcon sx={{minWidth: 36}}>{entity.icon}</ListItemIcon>
                <ListItemText primary={entity.name ?? "—"} secondary={entity.typeName} />
                <Stack sx={{alignItems: "flex-end"}}>
                  {entity.renderAdditionalSearchContent
                    ? entity.renderAdditionalSearchContent(entity)
                    : entity.renderAdditionalCardContent?.(entity)}
                </Stack>
              </ListItemButton>
            ))}
          </List>
        </>
      )}
    </DialogContent>
  );
}

function GlobalSearchModal({open, onClose}: GlobalSearchModalProps) {
  const inputRef = useRef<HTMLInputElement | null>(null);

  return (
    <Dialog
      open={open}
      onClose={onClose}
      maxWidth="sm"
      fullWidth
      slotProps={{
        transition: {onEntered: () => inputRef.current?.focus()},
        paper: {
          sx: {
            position: "fixed",
            top: "15%",
            m: 0,
            borderRadius: 2,
          },
        },
      }}
    >
      {open && <GlobalSearchContent onClose={onClose} inputRef={inputRef} />}
    </Dialog>
  );
}

export default GlobalSearchModal;
