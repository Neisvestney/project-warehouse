import LockOutlinedIcon from "@mui/icons-material/LockOutlined";
import ErrorPlaceholder from "./ErrorPlaceholder";

function AccessDenied() {
  return (
    <ErrorPlaceholder
      icon={LockOutlinedIcon}
      title="Доступ запрещён"
      description="У вас нет прав для просмотра этой страницы"
    />
  );
}

export default AccessDenied;
