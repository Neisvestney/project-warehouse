import {Box, Button, TextField, Typography} from "@mui/material";
import {useState} from "react";

function ThrowErrorPage() {
  const [message, setMessage] = useState("Test error from ThrowErrorPage");
  const [thrown, setThrown] = useState(false);

  if (thrown) {
    throw new Error(message);
  }

  return (
    <Box
      sx={{
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        minHeight: "60vh",
        gap: 3,
        p: 3,
      }}
    >
      <Typography variant="h5">Debug: throw error</Typography>
      <TextField
        label="Error message"
        value={message}
        onChange={(e) => setMessage(e.target.value)}
        sx={{width: 400}}
      />
      <Button variant="contained" color="error" onClick={() => setThrown(true)}>
        Throw Error
      </Button>
    </Box>
  );
}

export default ThrowErrorPage;
