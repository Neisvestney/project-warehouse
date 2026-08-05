import {Avatar, Box, Tooltip} from "@mui/material";
import OpenInNewIcon from "@mui/icons-material/OpenInNew";

interface CardImageProps {
  src?: string | null;
  name: string;
  size?: number;
}

function CardImage({src, name, size = 40}: CardImageProps) {
  const avatar = (
    <Avatar variant="rounded" src={src ?? undefined} sx={{width: size, height: size}}>
      {name.charAt(0)}
    </Avatar>
  );

  if (!src) return avatar;

  return (
    <Tooltip title="Открыть изображение">
      <Box
        component="a"
        href={src}
        target="_blank"
        rel="noopener noreferrer"
        // строка карточки кликабельна сама по себе — клик по картинке до неё доходить не должен
        onClick={(e) => e.stopPropagation()}
        sx={{
          position: "relative",
          display: "block",
          width: size,
          height: size,
          borderRadius: 1,
          overflow: "hidden",
          "&:hover .card-image-overlay": {opacity: 1},
        }}
      >
        {avatar}
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
          <OpenInNewIcon sx={{fontSize: Math.round(size / 2.5)}} />
        </Box>
      </Box>
    </Tooltip>
  );
}

export default CardImage;
