import {Avatar, Box, Tooltip} from "@mui/material";
import ZoomInIcon from "@mui/icons-material/ZoomIn";
import {useModal} from "@/hooks/useModal";
import FileImage from "@/components/files/FileImage";
import FileViewerModal from "@/components/files/viewer/FileViewerModal";
import {viewableUrl} from "@/components/files/viewer/viewableFile";

interface CardImageProps {
  src?: string | null;
  name: string;
  size?: number;
}

/** Marketplace card thumbnail — the one place in the app where the image lives on a foreign host. */
function CardImage({src, name, size = 40}: CardImageProps) {
  const {showModal} = useModal();

  const fallback = (
    <Avatar variant="rounded" sx={{width: size, height: size}}>
      {name.charAt(0)}
    </Avatar>
  );

  if (!src) return fallback;

  return (
    <Tooltip title="Открыть изображение">
      <Box
        // строка карточки кликабельна сама по себе — клик по картинке до неё доходить не должен
        onClick={(e) => {
          e.stopPropagation();
          void showModal(FileViewerModal, {files: [viewableUrl(src, {name})]});
        }}
        sx={{
          position: "relative",
          display: "block",
          width: size,
          height: size,
          borderRadius: 1,
          overflow: "hidden",
          cursor: "pointer",
          "&:hover .card-image-overlay": {opacity: 1},
        }}
      >
        <FileImage
          source={viewableUrl(src, {name})}
          style={{height: "100%", width: "100%", objectFit: "cover"}}
          fallback={fallback}
        />
        <Box
          className="card-image-overlay"
          sx={{
            position: "absolute",
            inset: 0,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            bgcolor: "rgba(0, 0, 0, 0.5)",
            color: "common.white",
            opacity: 0,
            transition: "opacity 0.15s",
          }}
        >
          <ZoomInIcon sx={{fontSize: Math.round(size / 2.5)}} />
        </Box>
      </Box>
    </Tooltip>
  );
}

export default CardImage;
