import type {ReactNode} from "react";
import {Box, Stack, Typography} from "@mui/material";
import type {AbcSubjectDto} from "@/api/types.gen";
import {useOpenCatalogItem} from "@/components/catalog/CatalogItemDrawerContext";
import CatalogItemLink from "@/components/catalog/CatalogItemLink";
import CardImage from "@/components/marketplace/CardImage.tsx";

interface AbcSubjectNameProps {
  subject: AbcSubjectDto;
  /** Rendered before the name, e.g. the rank. */
  prefix?: ReactNode;
  /** Appends the account names to the offer id, for views without an accounts column. */
  showAccounts?: boolean;
}

/** A catalog item links to its drawer; a card or an article shows its image and offer id. */
function AbcSubjectName({subject, prefix, showAccounts}: AbcSubjectNameProps) {
  const openCatalogItem = useOpenCatalogItem();

  const name = (
    <Typography variant="body2">
      {prefix != null && (
        <Box component="span" sx={{color: "text.secondary", mr: 0.75}}>
          {prefix}
        </Box>
      )}
      {subject.name}
    </Typography>
  );

  if (subject.catalogItemId)
    return (
      <CatalogItemLink catalogItemId={subject.catalogItemId} onOpen={openCatalogItem}>
        {name}
      </CatalogItemLink>
    );

  const caption = [
    subject.offerId,
    showAccounts && subject.accounts.map((a) => a.name).join(", "),
  ].filter(Boolean);

  return (
    <Stack direction="row" spacing={1} sx={{alignItems: "center"}}>
      <CardImage src={subject.imageUrl} name={subject.name} size={32} />
      <Box sx={{minWidth: 0}}>
        {name}
        <Typography variant="caption" color="text.secondary">
          {caption.join(" · ")}
        </Typography>
      </Box>
    </Stack>
  );
}

export default AbcSubjectName;
