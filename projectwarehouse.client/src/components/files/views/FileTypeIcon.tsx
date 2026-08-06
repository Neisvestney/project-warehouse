import ImageOutlinedIcon from "@mui/icons-material/ImageOutlined";
import PictureAsPdfOutlinedIcon from "@mui/icons-material/PictureAsPdfOutlined";
import TableChartOutlinedIcon from "@mui/icons-material/TableChartOutlined";
import DescriptionOutlinedIcon from "@mui/icons-material/DescriptionOutlined";
import ArticleOutlinedIcon from "@mui/icons-material/ArticleOutlined";
import InsertDriveFileOutlinedIcon from "@mui/icons-material/InsertDriveFileOutlined";
import type {SvgIconProps} from "@mui/material/SvgIcon";
import {iconKindForContentType} from "../fileUtils";

interface FileTypeIconProps extends SvgIconProps {
  contentType?: string | null;
}

export default function FileTypeIcon({contentType, ...props}: FileTypeIconProps) {
  switch (iconKindForContentType(contentType)) {
    case "image":
      return <ImageOutlinedIcon {...props} />;
    case "pdf":
      return <PictureAsPdfOutlinedIcon {...props} />;
    case "spreadsheet":
      return <TableChartOutlinedIcon {...props} />;
    case "document":
      return <DescriptionOutlinedIcon {...props} />;
    case "text":
      return <ArticleOutlinedIcon {...props} />;
    default:
      return <InsertDriveFileOutlinedIcon {...props} />;
  }
}
