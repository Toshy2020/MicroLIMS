import { apiClient } from "../../../services/apiClient";
import {
  DashboardSummary, KpiDeltas, MonthlyTrendPoint, DistributionSlice,
  NotificationItem, MyTask, MediaExpiryLot, TodaysWorkItem, IncubationOverviewRow, AnalystMetrics,
  SectionHeadDashboard, ReviewerDashboard
} from "../types/dashboard";
import { DashboardLabCode, labQuery } from "../DashboardLabContext";

// Every dashboard call takes the laboratory the view is for; the server
// narrows its figures to that laboratory's sections (DashboardLabScopeService).
// null keeps the previous all-of-my-labs behaviour.
type Lab = DashboardLabCode | null;

export const DashboardService = {
  async getSummary(lab: Lab = null): Promise<DashboardSummary> {
    return (await apiClient.get(`/dashboard${labQuery(lab)}`)).data.data;
  },
  async getSectionHeadDashboard(lab: Lab = null): Promise<SectionHeadDashboard> {
    return (await apiClient.get(`/dashboard/section-head${labQuery(lab)}`)).data.data;
  },
  async getReviewerDashboard(lab: Lab = null): Promise<ReviewerDashboard> {
    return (await apiClient.get(`/dashboard/reviewer${labQuery(lab)}`)).data.data;
  },
  async getKpiDeltas(lab: Lab = null): Promise<KpiDeltas> {
    return (await apiClient.get(`/dashboard/kpi-deltas${labQuery(lab)}`)).data.data;
  },
  async getMonthlyTrend(months: number, lab: Lab = null): Promise<MonthlyTrendPoint[]> {
    return (await apiClient.get(`/dashboard/monthly-trend?months=${months}${labQuery(lab, "&")}`)).data.data;
  },
  async getStatusDistribution(lab: Lab = null): Promise<DistributionSlice[]> {
    return (await apiClient.get(`/dashboard/status-distribution${labQuery(lab)}`)).data.data;
  },
  async getCategoryDistribution(lab: Lab = null): Promise<DistributionSlice[]> {
    return (await apiClient.get(`/dashboard/category-distribution${labQuery(lab)}`)).data.data;
  },
  async getNotifications(lab: Lab = null): Promise<NotificationItem[]> {
    return (await apiClient.get(`/dashboard/notifications${labQuery(lab)}`)).data.data;
  },
  async markNotificationRead(id: number): Promise<void> {
    await apiClient.post(`/dashboard/notifications/${id}/read`);
  },
  async getMyTasks(lab: Lab = null): Promise<MyTask[]> {
    return (await apiClient.get(`/dashboard/my-tasks${labQuery(lab)}`)).data.data;
  },
  async getTodaysWork(lab: Lab = null): Promise<TodaysWorkItem[]> {
    return (await apiClient.get(`/dashboard/todays-work${labQuery(lab)}`)).data.data;
  },
  async getIncubationOverview(myIncubationsOnly = false, lab: Lab = null): Promise<IncubationOverviewRow[]> {
    const url = myIncubationsOnly ? "/dashboard/incubation-overview?myIncubationsOnly=true" : "/dashboard/incubation-overview?myIncubationsOnly=false";
    return (await apiClient.get(`${url}${labQuery(lab, "&")}`)).data.data;
  },
  // Prepared media are Microbiology material; never called for a
  // Physicochemical view.
  async getMediaExpiry(withinDays = 7): Promise<MediaExpiryLot[]> {
    return (await apiClient.get(`/media/expiring?withinDays=${withinDays}`)).data.data;
  },
  async getAnalystMetrics(lab: Lab = null): Promise<AnalystMetrics> {
    return (await apiClient.get(`/dashboard/analyst-metrics${labQuery(lab)}`)).data.data;
  }
};
