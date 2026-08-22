import { apiClient } from "../../../services/apiClient";
import {
  DashboardSummary, KpiDeltas, MonthlyTrendPoint, DistributionSlice,
  NotificationItem, MyTask, MediaExpiryLot, TodaysWorkItem, IncubationOverviewRow, AnalystMetrics,
  SectionHeadDashboard, ReviewerDashboard
} from "../types/dashboard";

export const DashboardService = {
  async getSummary(): Promise<DashboardSummary> {
    return (await apiClient.get("/dashboard")).data.data;
  },
  async getSectionHeadDashboard(): Promise<SectionHeadDashboard> {
    return (await apiClient.get("/dashboard/section-head")).data.data;
  },
  async getReviewerDashboard(): Promise<ReviewerDashboard> {
    return (await apiClient.get("/dashboard/reviewer")).data.data;
  },
  async getKpiDeltas(): Promise<KpiDeltas> {
    return (await apiClient.get("/dashboard/kpi-deltas")).data.data;
  },
  async getMonthlyTrend(months: number): Promise<MonthlyTrendPoint[]> {
    return (await apiClient.get(`/dashboard/monthly-trend?months=${months}`)).data.data;
  },
  async getStatusDistribution(): Promise<DistributionSlice[]> {
    return (await apiClient.get("/dashboard/status-distribution")).data.data;
  },
  async getCategoryDistribution(): Promise<DistributionSlice[]> {
    return (await apiClient.get("/dashboard/category-distribution")).data.data;
  },
  async getNotifications(): Promise<NotificationItem[]> {
    return (await apiClient.get("/dashboard/notifications")).data.data;
  },
  async markNotificationRead(id: number): Promise<void> {
    await apiClient.post(`/dashboard/notifications/${id}/read`);
  },
  async getMyTasks(): Promise<MyTask[]> {
    return (await apiClient.get("/dashboard/my-tasks")).data.data;
  },
  async getTodaysWork(): Promise<TodaysWorkItem[]> {
    return (await apiClient.get("/dashboard/todays-work")).data.data;
  },
  async getIncubationOverview(myIncubationsOnly = false): Promise<IncubationOverviewRow[]> {
    const url = myIncubationsOnly ? "/dashboard/incubation-overview?myIncubationsOnly=true" : "/dashboard/incubation-overview";
    return (await apiClient.get(url)).data.data;
  },
  async getMediaExpiry(withinDays = 7): Promise<MediaExpiryLot[]> {
    return (await apiClient.get(`/media/expiring?withinDays=${withinDays}`)).data.data;
  },
  async getAnalystMetrics(): Promise<AnalystMetrics> {
    return (await apiClient.get("/dashboard/analyst-metrics")).data.data;
  }
};
