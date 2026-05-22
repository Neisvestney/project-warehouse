import {
  Chip,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from "@mui/material";
import {type ItemsGroupDto} from "@/api/types.gen.ts";

interface ItemsGroupsTableProps {
  groups: ItemsGroupDto[];
  emptyMessage?: string;
}

function ItemsGroupsTable({groups, emptyMessage = "Нет товаров"}: ItemsGroupsTableProps) {
  if (groups.length === 0) {
    return (
      <Typography variant="body2" color="text.secondary">
        {emptyMessage}
      </Typography>
    );
  }

  return (
    <TableContainer>
      <Table size="small">
        <TableHead>
          <TableRow>
            <TableCell>Товар</TableCell>
            <TableCell>Характеристика</TableCell>
            <TableCell align="right">Кол-во</TableCell>
          </TableRow>
        </TableHead>
        <TableBody>
          {groups.map((group) => (
            <TableRow key={group.id}>
              <TableCell>
                <Typography variant="body2">
                  {group.catalogItemWithCharacteristic.catalogItem.name}
                </Typography>
                <Typography variant="caption" color="text.secondary">
                  {group.catalogItemWithCharacteristic.catalogItem.article}
                </Typography>
              </TableCell>
              <TableCell>{group.catalogItemWithCharacteristic.characteristic || "—"}</TableCell>
              <TableCell align="right">
                <Chip label={group.count} size="small" color="primary" variant="outlined" />
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  );
}

export default ItemsGroupsTable;
