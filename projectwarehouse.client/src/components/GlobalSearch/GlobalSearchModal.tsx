import React, {Suspense, lazy, useMemo, useRef, useState} from "react";
import {
  Box,
  CircularProgress,
  Dialog,
  DialogContent,
  Divider,
  IconButton,
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
import CameraAltIcon from "@mui/icons-material/CameraAlt";
import NoPhotographyIcon from "@mui/icons-material/NoPhotography";
import {useQuery} from "@tanstack/react-query";
import {useNavigate} from "react-router";
import {commonContentGlobalSearchOptions} from "@/api/@tanstack/react-query.gen";
import {resolveEntity} from "@/utils/appEntityUtils";
import {isCameraApiSupported} from "@/utils/camera/cameraUtils";
import {useDebounce} from "@/hooks/useDebounce";
import {useBackClosable} from "@/hooks/useBackClosable.ts";
import {useRetainedValue} from "@/hooks/useRetainedValue";
import type {AppEntity} from "@/api";
import type {GlobalSearchRequest} from "@/contexts/GlobalSearch/GlobalSearchContext";
import {getScannedEntityLink} from "./scannedEntityLink";
import {extractMarkGtin} from "@/utils/gtinUtils";

const ScannerBlock = lazy(() => import("@/components/ScannerBlock/ScannerBlock"));

type ResolvedEntity = ReturnType<typeof resolveEntity>;

/** `seq` is unique per open call, so a repeated request restarts the content even with the same query. */
export type GlobalSearchModalRequest = GlobalSearchRequest & {seq: number};

interface GlobalSearchModalProps {
  /** `null` closes the modal. */
  request: GlobalSearchModalRequest | null;
  onClose: () => void;
}

function GlobalSearchContent({
  onClose,
  inputRef,
  request,
}: {
  onClose: () => void;
  inputRef: React.RefObject<HTMLInputElement | null>;
  request: GlobalSearchRequest;
}) {
  const [inputValue, setInputValue] = useState(request.query ?? "");
  const [cameraOpen, setCameraOpen] = useState(!!request.camera && isCameraApiSupported());
  const [activeIndex, setActiveIndex] = useState(-1);
  const debouncedSearch = useDebounce(inputValue, 300);
  const navigate = useNavigate();
  const itemRefs = useRef<(HTMLLIElement | null)[]>([]);
  // ScannerBlock keeps reporting the same code every frame until it unmounts
  const scanHandledRef = useRef(false);

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
    navigate(entity.link, {replace: true});
    onClose();
  };

  const handleScanned = (raw: string) => {
    if (scanHandledRef.current) return;
    scanHandledRef.current = true;

    const link = getScannedEntityLink(raw);
    if (link) {
      navigate(link, {replace: true});
      onClose();
      return;
    }
    setInputValue(extractMarkGtin(raw) ?? raw.trim());
    setActiveIndex(-1);
    setCameraOpen(false);
  };

  const toggleCamera = () => {
    scanHandledRef.current = false;
    setCameraOpen((v) => !v);
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

  const showList = !cameraOpen && debouncedSearch.trim().length > 0;
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
            endAdornment: isCameraApiSupported() && (
              <InputAdornment position="end">
                <IconButton
                  edge="end"
                  onClick={toggleCamera}
                  aria-label={cameraOpen ? "Выключить камеру" : "Сканировать камерой"}
                >
                  {cameraOpen ? <NoPhotographyIcon /> : <CameraAltIcon />}
                </IconButton>
              </InputAdornment>
            ),
          },
        }}
        sx={{
          "& .MuiOutlinedInput-root": {
            borderRadius: showList || cameraOpen ? "8px 8px 0 0" : 2,
            "& fieldset": {border: "none"},
          },
        }}
      />
      {cameraOpen && (
        <>
          <Divider />
          <Box sx={{height: {xs: "60vh", sm: 360}}}>
            <Suspense
              fallback={
                <Stack sx={{height: "100%", alignItems: "center", justifyContent: "center"}}>
                  <CircularProgress size={32} />
                </Stack>
              }
            >
              <ScannerBlock onScanned={handleScanned} />
            </Suspense>
          </Box>
        </>
      )}
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

function GlobalSearchModal({request, onClose}: GlobalSearchModalProps) {
  const inputRef = useRef<HTMLInputElement | null>(null);
  const open = !!request;

  useBackClosable(open, onClose);

  const [shownRequest, releaseShown] = useRetainedValue(request);

  return (
    <Dialog
      open={open}
      onClose={onClose}
      maxWidth="sm"
      fullWidth
      slotProps={{
        // Focusing in camera mode would pop the on-screen keyboard over the viewfinder
        transition: {
          onEntered: () => {
            if (!shownRequest?.camera) inputRef.current?.focus();
          },
          onExited: releaseShown,
        },
        paper: {
          sx: {
            position: "fixed",
            top: "15%",
            m: 0,
            borderRadius: 2,
            pointerEvents: open ? undefined : "none",
          },
        },
      }}
    >
      {shownRequest && (
        <GlobalSearchContent
          key={shownRequest.seq}
          onClose={onClose}
          inputRef={inputRef}
          request={shownRequest}
        />
      )}
    </Dialog>
  );
}

export default GlobalSearchModal;
