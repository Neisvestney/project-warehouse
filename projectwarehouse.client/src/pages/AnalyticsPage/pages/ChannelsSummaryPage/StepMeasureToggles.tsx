import {ToggleButton, ToggleButtonGroup} from "@mui/material";
import type {AnalyticsMeasure, AnalyticsStep} from "@/api/types.gen";

interface StepMeasureTogglesProps {
  /** The step applied; null while the first response has not told which one the server picked. */
  step: AnalyticsStep | null;
  onStepChange: (value: AnalyticsStep) => void;
  /** Left out for a chart that counts one thing only. */
  measure?: AnalyticsMeasure;
  onMeasureChange?: (value: AnalyticsMeasure) => void;
}

function StepMeasureToggles({
  step,
  onStepChange,
  measure,
  onMeasureChange,
}: StepMeasureTogglesProps) {
  return (
    <>
      <ToggleButtonGroup
        exclusive
        size="small"
        value={step}
        onChange={(_, value: AnalyticsStep | null) => value && onStepChange(value)}
      >
        <ToggleButton value="day">День</ToggleButton>
        <ToggleButton value="week">Неделя</ToggleButton>
        <ToggleButton value="month">Месяц</ToggleButton>
      </ToggleButtonGroup>
      {measure && onMeasureChange && (
        <ToggleButtonGroup
          exclusive
          size="small"
          value={measure}
          onChange={(_, value: AnalyticsMeasure | null) => value && onMeasureChange(value)}
        >
          <ToggleButton value="orders">Заказы</ToggleButton>
          <ToggleButton value="units">Штуки</ToggleButton>
        </ToggleButtonGroup>
      )}
    </>
  );
}

export default StepMeasureToggles;
