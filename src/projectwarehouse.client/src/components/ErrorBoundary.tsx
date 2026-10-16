import React from "react";
import {Box, Button, CircularProgress, Typography} from "@mui/material";
import ErrorOutlineIcon from "@mui/icons-material/ErrorOutlineOutlined";
import {checkForServiceWorkerUpdate} from "@/services/serviceWorkerUpdate";

interface Props {
  children: React.ReactNode;
}

interface State {
  hasError: boolean;
  updating: boolean;
  countdown: number | null;
}

class ErrorBoundary extends React.Component<Props, State> {
  private countdownTimer: ReturnType<typeof setInterval> | null = null;

  state: State = {hasError: false, updating: false, countdown: null};

  static getDerivedStateFromError(): Partial<State> {
    return {hasError: true};
  }

  componentDidCatch() {
    this.tryUpdateServiceWorker();
  }

  private async tryUpdateServiceWorker() {
    if (!("serviceWorker" in navigator)) return;

    this.setState({updating: true});

    try {
      // A crash is worth a check even right after a throttled one: a broken bundle is the symptom.
      const registration = await checkForServiceWorkerUpdate({force: true});

      const waiting = registration?.waiting;
      if (waiting) {
        waiting.postMessage({type: "SKIP_WAITING"});
        this.startCountdown();
      }
    } catch {
      // Nothing left to try — the user can still reload by hand.
    } finally {
      this.setState({updating: false});
    }
  }

  private startCountdown() {
    this.setState({countdown: 3});
    this.countdownTimer = setInterval(() => {
      this.setState((prev) => {
        const next = (prev.countdown ?? 1) - 1;
        if (next <= 0) {
          clearInterval(this.countdownTimer!);
          window.location.reload();
          return {countdown: 0};
        }
        return {countdown: next};
      });
    }, 1000);
  }

  componentWillUnmount() {
    if (this.countdownTimer) clearInterval(this.countdownTimer);
  }

  private handleReload = () => {
    window.location.reload();
  };

  render() {
    if (!this.state.hasError) return this.props.children;

    const {updating, countdown} = this.state;

    return (
      <Box
        sx={{
          position: "fixed",
          inset: 0,
          display: "flex",
          flexDirection: "column",
          alignItems: "center",
          justifyContent: "center",
          gap: 3,
          p: 3,
          bgcolor: "background.default",
        }}
      >
        <ErrorOutlineIcon sx={{fontSize: 72, color: "error.main"}} />
        <Typography variant="h5" sx={{textAlign: "center"}}>
          Произошла непредвиденная ошибка
        </Typography>
        <Typography variant="body2" color="text.secondary" sx={{textAlign: "center"}}>
          {updating
            ? "Проверяем обновления приложения…"
            : countdown !== null
              ? `Перезагружаем через ${countdown}…`
              : "Попробуйте перезагрузить страницу"}
        </Typography>
        {(updating || countdown !== null) && <CircularProgress size={24} />}
        <Button
          variant="outlined"
          onClick={this.handleReload}
          disabled={countdown !== null && countdown > 0}
        >
          Перезагрузить сейчас
        </Button>
      </Box>
    );
  }
}

export default ErrorBoundary;
