// The server builds each recent row's route (RecordNavigationTargets, as for
// search results), so a test order opens through its sample. Only a path inside
// this application is followed; anything else leaves the row out.
export function inAppRoute(route: string | null | undefined): string | null {
  if (!route || !route.startsWith("/") || route.startsWith("//")) return null;
  return route;
}
