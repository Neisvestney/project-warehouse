import React from "react";
import {
  LinearProgress,
  Paper,
  type PaperProps,
  TableContainer,
  TablePagination,
  useMediaQuery,
} from "@mui/material";
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
  children,
  ...paperProps
}: DataTableContainerProps) {
  const isMobile = useMediaQuery(theme.breakpoints.down("sm"));

  return (
    <Paper {...paperProps}>
      <LinearProgress
        sx={{visibility: isFetching ? "visible" : "hidden", borderRadius: "4px 4px 0 0"}}
      />
      <TableContainer>{children}</TableContainer>
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
    </Paper>
  );
}

export default DataTableContainer;
