import { Component, ErrorInfo, ReactNode } from "react";
import { Box, Button, Paper, Stack, Typography } from "@mui/material";
import { reportClientError } from "../services/errorReporter";

interface Props {
  children: ReactNode;
  // Page-level boundary inside the app shell: renders in the content area
  // so the sidebar and topbar stay usable and the user can navigate away.
  // MainLayout keys it on the route, so moving to another page resets it.
  inline?: boolean;
}

interface State {
  hasError: boolean;
}

// Catches render-time crashes anywhere below it. Before this existed a
// thrown error unmounted the whole tree and left the analyst on a blank
// white screen, with nothing recorded anywhere.
//
// Class component by necessity - React exposes no hook equivalent of
// componentDidCatch.
export class AppErrorBoundary extends Component<Props, State> {
  state: State = { hasError: false };

  static getDerivedStateFromError(): State {
    return { hasError: true };
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo): void {
    // Fire-and-forget; reportClientError swallows its own failures so a
    // reporting problem cannot re-enter this boundary.
    reportClientError({
      error,
      source: "errorBoundary",
      componentStack: errorInfo.componentStack ?? undefined
    });
  }

  private handleReload = (): void => {
    window.location.reload();
  };

  render(): ReactNode {
    if (!this.state.hasError) return this.props.children;

    // Intentionally plain: this renders when the app is already broken, so
    // it depends on as little as possible - no theme tokens, no router, no
    // context. It also shows no error detail, which belongs in the admin
    // error log rather than on an analyst's screen.
    const { inline } = this.props;
    return (
      <Box
        role="alert"
        sx={{
          minHeight: inline ? undefined : "100vh",
          display: "flex",
          alignItems: inline ? "flex-start" : "center",
          justifyContent: "center",
          p: 3
        }}
      >
        <Paper sx={{ p: 4, maxWidth: 520 }} {...(inline ? { variant: "outlined" as const } : { elevation: 3 })}>
          <Stack spacing={2}>
            <Typography variant="h6" component="h1">
              {inline ? "This page could not be displayed" : "Something went wrong"}
            </Typography>
            <Typography variant="body2" sx={{
              color: "text.secondary"
            }}>
              {inline
                ? "An unexpected problem stopped this page from loading. The problem has been reported to your system administrator automatically. No work you had already saved is affected - you can reload the page or use the navigation to continue elsewhere."
                : "This screen stopped responding and could not recover. The problem has been reported to your system administrator automatically. No work you had already saved is affected."}
            </Typography>
            <Box>
              <Button variant="contained" onClick={this.handleReload} sx={{ textTransform: "none" }}>
                Reload the page
              </Button>
            </Box>
          </Stack>
        </Paper>
      </Box>
    );
  }
}
