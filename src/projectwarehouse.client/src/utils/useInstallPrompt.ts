import {useEffect, useState} from "react";

interface BeforeInstallPromptEvent extends Event {
  prompt: () => Promise<void>;
  userChoice: Promise<{outcome: "accepted" | "dismissed"}>;
}

export const useInstallPrompt = () => {
  const [deferredPrompt, setDeferredPrompt] = useState<BeforeInstallPromptEvent | null>(null);

  useEffect(() => {
    const handler = (e: Event) => {
      e.preventDefault();
      setDeferredPrompt(e as BeforeInstallPromptEvent);
    };

    window.addEventListener("beforeinstallprompt", handler);

    return () => {
      window.removeEventListener("beforeinstallprompt", handler);
    };
  }, []);

  const triggerInstall = async () => {
    if (!deferredPrompt) return false;

    await deferredPrompt.prompt();
    const result = await deferredPrompt.userChoice;

    setDeferredPrompt(null);

    return result.outcome === "accepted";
  };

  return {
    canInstall: !!deferredPrompt,
    triggerInstall,
  };
};
