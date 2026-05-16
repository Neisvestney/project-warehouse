import React from "react";
import {observer} from "mobx-react-lite";
import {ToggleButton, ToggleButtonGroup, Tooltip} from "@mui/material";
import NearMeIcon from "@mui/icons-material/NearMe";
import InventoryIcon from "@mui/icons-material/Inventory";
import HorizontalRuleIcon from "@mui/icons-material/HorizontalRule";
import DirectionsWalkIcon from "@mui/icons-material/DirectionsWalk";
import type {Tool} from "../warehouseEditStore";
import {useWarehouseEditStore} from "../WarehouseEditStoreContext";

const TOOLS: {value: Tool; label: string; icon: React.ReactElement}[] = [
  {value: "select", label: "Выбрать / Переместить", icon: <NearMeIcon fontSize="small" />},
  {value: "storagePlace", label: "Место хранения", icon: <InventoryIcon fontSize="small" />},
  {value: "wall", label: "Стена", icon: <HorizontalRuleIcon fontSize="small" />},
  {value: "passage", label: "Проход", icon: <DirectionsWalkIcon fontSize="small" />},
];

export default observer(function WarehouseEditToolbar() {
  const store = useWarehouseEditStore();

  return (
    <>
      <ToggleButtonGroup
        value={store.activeTool}
        exclusive
        onChange={(_, v) => v && store.setActiveTool(v as Tool)}
        size="small"
      >
        {TOOLS.map((t) => (
          <Tooltip key={t.value} title={t.label} placement="bottom">
            <ToggleButton value={t.value} sx={{px: 1.5}}>
              {t.icon}
            </ToggleButton>
          </Tooltip>
        ))}
      </ToggleButtonGroup>
    </>
  );
});
