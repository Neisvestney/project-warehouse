Before planning read `docs/` to understand project structure.  
Project uses MUI v9 that has some breaking changes
When planning new features or code edits add docs writing as final step.  
Check typescript with `npm run typecheck` command (run from `projectwarehouse.client`; uses TypeScript 7 / native Go compiler) to catch any compilation errors.  
After you edited any of frontend code run prettier and linter: `npm run prettier:fix` and `eslint:fix` in `projectwarehouse.client` directory.