import {useState} from "react";
import {Box, Button, IconButton, Tooltip} from "@mui/material";
import InfoOutlinedIcon from "@mui/icons-material/InfoOutlined";
import RefreshIcon from "@mui/icons-material/Refresh";
import SettingsIcon from "@mui/icons-material/Settings";
import AnalyticsSettingsDialog, {
  type AnalyticsSettingsGroup,
} from "@/components/analytics/AnalyticsSettingsDialog";
import PageGenericHeader from "@/components/PageGenericHeader";
import {useHasPermission} from "@/hooks/usePermission";

interface AnalyticsPageHeaderProps {
  title: string;
  /** Parameters the numbers were computed with, from the last response; undefined until it arrives. */
  appliedSettings: string[] | undefined;
  onRefresh: () => void;
  settingsGroup: AnalyticsSettingsGroup;
}

function AnalyticsPageHeader({
  title,
  appliedSettings,
  onRefresh,
  settingsGroup,
}: AnalyticsPageHeaderProps) {
  const canEditSettings = useHasPermission("analytics.settings");
  const [settingsOpen, setSettingsOpen] = useState(false);

  return (
    <>
      <PageGenericHeader
        title={
          // Applied parameters hang off a permanently rendered icon so the header does not jump on reload
          <>
            {title}
            <Tooltip
              title={appliedSettings && appliedSettings.map((line) => <Box key={line}>{line}</Box>)}
            >
              <InfoOutlinedIcon
                sx={{
                  fontSize: "0.7em",
                  verticalAlign: "middle",
                  ml: 0.5,
                  color: "primary.main",
                  opacity: appliedSettings ? 1 : 0,
                  pointerEvents: appliedSettings ? "auto" : "none",
                }}
              />
            </Tooltip>
          </>
        }
        refresh={
          <Tooltip title="Обновить">
            <IconButton color="inherit" onClick={onRefresh}>
              <RefreshIcon />
            </IconButton>
          </Tooltip>
        }
        actions={
          canEditSettings && (
            <Button
              variant="outlined"
              startIcon={<SettingsIcon />}
              onClick={() => setSettingsOpen(true)}
            >
              Настройки
            </Button>
          )
        }
      />
      <AnalyticsSettingsDialog
        open={settingsOpen}
        group={settingsGroup}
        onClose={() => setSettingsOpen(false)}
      />
    </>
  );
}

export default AnalyticsPageHeader;
