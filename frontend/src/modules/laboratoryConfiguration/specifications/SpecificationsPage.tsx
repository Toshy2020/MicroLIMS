import React from "react";
import { Navigate } from "react-router-dom";

export const SpecificationsPage: React.FC = () => {
  return <Navigate to="/laboratory-configuration/items" replace />;
};
