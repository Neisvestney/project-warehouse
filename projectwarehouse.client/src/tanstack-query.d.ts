import "@tanstack/react-query";

declare module "@tanstack/react-query" {
  interface Register {
    mutationMeta: {
      suppressGlobalError?: boolean;
    };
    queryMeta: {
      suppressGlobalError?: boolean;
    };
  }
}
