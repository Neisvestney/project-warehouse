import ErrorOutlinedIcon from "@mui/icons-material/ErrorOutlined";
import {extractErrorMessage} from "@/utils/errorUtils";
import ErrorPlaceholder from "./ErrorPlaceholder";

interface QueryErrorProps {
  error?: unknown;
}

function QueryError({error}: QueryErrorProps) {
  return (
    <ErrorPlaceholder
      icon={ErrorOutlinedIcon}
      title="Произошла ошибка"
      description={error !== undefined ? extractErrorMessage(error) : undefined}
    />
  );
}

export default QueryError;
