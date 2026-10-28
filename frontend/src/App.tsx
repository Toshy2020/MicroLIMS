import { AppRoutes } from "./routes/AppRoutes";
import { SystemStatusBanner } from "./components/SystemStatusBanner";

// The frontend never decides laboratory rules (Frozen Principle #3).
// It only renders routes and lets each module call the backend API.
export default function App() {
  return (
    <>
      <SystemStatusBanner />
      <AppRoutes />
    </>
  );
}
