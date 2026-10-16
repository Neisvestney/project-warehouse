import React, {useEffect, useRef, useState} from "react";
import {
  Box,
  Fade,
  IconButton,
  LinearProgress,
  Paper,
  type PaperProps,
  Skeleton,
  TableContainer,
  TablePagination,
  Typography,
  useMediaQuery,
} from "@mui/material";
import KeyboardArrowLeftIcon from "@mui/icons-material/KeyboardArrowLeft";
import KeyboardArrowRightIcon from "@mui/icons-material/KeyboardArrowRight";
import theme from "@/theme.ts";

type DataTableContainerProps = Omit<PaperProps, "children"> & {
  isFetching: boolean;
  /** 1-based page number */
  count: number;
  page: number;
  onPageChange: (page: number) => void;
  rowsPerPage: number;
  onRowsPerPageChange: (rowsPerPage: number) => void;
  rowsPerPageOptions?: number[];
  /** Hides the floating pill shown while the real pagination is off-screen. */
  disableFloatingPagination?: boolean;
  children: React.ReactNode;
};

function DataTableContainer({
  isFetching,
  count,
  page,
  onPageChange,
  rowsPerPage,
  onRowsPerPageChange,
  rowsPerPageOptions = [10, 20, 50],
  disableFloatingPagination = false,
  children,
  ...paperProps
}: DataTableContainerProps) {
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));

  const paginationRef = useRef<HTMLDivElement>(null);
  const [paginationVisible, setPaginationVisible] = useState(false);

  useEffect(() => {
    const node = paginationRef.current;
    if (!node || disableFloatingPagination) return;

    // fully visible, otherwise the pill hides while the real controls are still a sliver off-screen
    const observer = new IntersectionObserver(
      ([entry]) => setPaginationVisible(entry.isIntersecting),
      {threshold: 1},
    );
    observer.observe(node);
    return () => observer.disconnect();
  }, [disableFloatingPagination]);

  const lastPage = Math.max(1, Math.ceil(count / rowsPerPage));
  const from = count === 0 ? 0 : (page - 1) * rowsPerPage + 1;
  const to = Math.min(count, page * rowsPerPage);
  const showFloating = !disableFloatingPagination && !paginationVisible && count > rowsPerPage;
  // a background refetch keeps the previous totals, so only a first load has nothing to show
  const showSkeleton = isFetching && count === 0;

  return (
    <Paper {...paperProps}>
      <LinearProgress
        sx={{visibility: isFetching ? "visible" : "hidden", borderRadius: "4px 4px 0 0"}}
      />
      <TableContainer>{children}</TableContainer>
      <Box ref={paginationRef}>
        {showSkeleton ? (
          <Box
            sx={{
              display: "flex",
              alignItems: "center",
              justifyContent: "flex-end",
              gap: 2,
              minHeight: 52,
              px: 2,
            }}
          >
            {!isMobile && <Skeleton variant="text" width={160} />}
            <Skeleton variant="text" width={110} />
            <Skeleton variant="circular" width={28} height={28} />
            <Skeleton variant="circular" width={28} height={28} />
          </Box>
        ) : (
          <TablePagination
            component="div"
            count={count}
            page={page - 1}
            rowsPerPage={rowsPerPage}
            rowsPerPageOptions={[
              ...new Set([rowsPerPage, ...rowsPerPageOptions].sort((a, b) => a - b)),
            ]}
            onPageChange={(_, newPage) => onPageChange(newPage + 1)}
            onRowsPerPageChange={(e) => onRowsPerPageChange(Number(e.target.value))}
            labelRowsPerPage={isMobile ? "" : "Строк на странице:"}
            labelDisplayedRows={({from, to, count}) => `${from}–${to} из ${count}`}
          />
        )}
      </Box>

      <Fade in={showFloating} unmountOnExit>
        <Paper
          elevation={6}
          component="nav"
          aria-label="Пагинация"
          sx={{
            position: "fixed",
            right: 16,
            bottom: 16,
            zIndex: (t) => t.zIndex.fab,
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            gap: 0.5,
            py: 0.5,
            px: 1,
            borderRadius: 6,
            minWidth: 180,
          }}
        >
          <IconButton
            size="small"
            aria-label="Предыдущая страница"
            disabled={isFetching || page <= 1}
            onClick={() => onPageChange(page - 1)}
          >
            <KeyboardArrowLeftIcon fontSize="small" />
          </IconButton>
          {showSkeleton ? (
            <Skeleton variant="text" width={90} />
          ) : (
            <Typography variant="caption" aria-live="polite" sx={{whiteSpace: "nowrap"}}>
              {from}–{to} из {count}
            </Typography>
          )}
          <IconButton
            size="small"
            aria-label="Следующая страница"
            disabled={isFetching || page >= lastPage}
            onClick={() => onPageChange(page + 1)}
          >
            <KeyboardArrowRightIcon fontSize="small" />
          </IconButton>
        </Paper>
      </Fade>
    </Paper>
  );
}

export default DataTableContainer;
