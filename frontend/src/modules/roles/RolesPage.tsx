import { PageHeader } from "../../components/PageHeader";
import { SectionTitle } from "../../components/SectionTitle";
import { RoleTable } from "./RoleTable";

export function RolesPage() {
  return (
    <>
      <PageHeader title="Roles" subtitle="System Administrator, Section Head, Reviewer, Analyst." />
      <SectionTitle>All Roles</SectionTitle>
      <RoleTable />
    </>
  );
}
