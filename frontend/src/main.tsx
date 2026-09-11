import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter } from "react-router-dom";
import App from "./App";
import { ThemeModeProvider } from "./theme/ThemeModeContext";
import { AuthProvider } from "./contexts/AuthContext";
import { AppErrorBoundary } from "./components/AppErrorBoundary";
import { installGlobalErrorHandlers } from "./services/errorReporter";

// Catches what React never sees: async callbacks, event handlers and
// rejected promises. Installed before render so a crash during the very
// first mount is still reported.
installGlobalErrorHandlers();

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    {/* Outside the providers on purpose: a crash in ThemeModeProvider or
        AuthProvider is exactly the kind that used to white-screen the app,
        and a boundary nested inside them could not catch it. */}
    <AppErrorBoundary>
      <ThemeModeProvider>
        <BrowserRouter>
          <AuthProvider>
            <App />
          </AuthProvider>
        </BrowserRouter>
      </ThemeModeProvider>
    </AppErrorBoundary>
  </React.StrictMode>
);
