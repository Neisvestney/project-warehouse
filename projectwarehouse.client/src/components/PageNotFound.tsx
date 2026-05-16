import WebAssetOffIcon from "@mui/icons-material/WebAssetOff";
import ErrorPlaceholder from "./ErrorPlaceholder";

function PageNotFound() {
  return (
    <ErrorPlaceholder
      icon={WebAssetOffIcon}
      title="Страница не найдена"
      description="Страница не существует или была перемещена"
    />
  );
}

export default PageNotFound;
