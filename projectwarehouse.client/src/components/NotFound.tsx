import SearchOffIcon from "@mui/icons-material/SearchOff";
import ErrorPlaceholder from "./ErrorPlaceholder";

function NotFound() {
  return (
    <ErrorPlaceholder
      icon={SearchOffIcon}
      title="Ресурс не найден"
      description="Запрошенный ресурс не существует или был удалён"
    />
  );
}

export default NotFound;
