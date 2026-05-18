export interface ServerConfig {
  name: string;
  url: string;
}

export const SELECTED_SERVER_KEY = "selected_server_url";

export const PREDEFINED_SERVERS: ServerConfig[] = [
  // Добавлять предустановленные серверы сюда
  // { name: "Основной склад", url: "https://warehouse.company.ru" },
];
