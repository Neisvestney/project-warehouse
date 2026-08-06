import Box from "@mui/material/Box";
import {Capacitor} from "@capacitor/core";
import UnsupportedFileRenderer from "./UnsupportedFileRenderer";
import type {ResolvedViewable} from "./useViewableSource";

/**
 * The browser's built-in PDF viewer in an iframe. No PDF library is added: @vitejs/plugin-legacy
 * targets chrome >= 49, which modern pdf.js builds do not support.
 *
 * Two cases fall back to the plain download card instead:
 * - the native client (Android 7 WebView renders no PDF in an iframe, showing a blank frame);
 * - external PDFs, which X-Frame-Options or frame-ancestors may block with no error event at all —
 *   an empty frame the user cannot explain is worse than an explicit button.
 */
export default function PdfFileRenderer({item}: {item: ResolvedViewable}) {
  if (Capacitor.isNativePlatform() || item.isExternal || !item.src)
    return <UnsupportedFileRenderer item={item} />;

  return (
    <Box
      component="iframe"
      src={item.src}
      title={item.name}
      sx={{width: "100%", height: "100%", border: 0, bgcolor: "common.white"}}
    />
  );
}
