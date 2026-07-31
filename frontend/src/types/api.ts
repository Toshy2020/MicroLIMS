// Mirrors backend Shared/Responses/ApiResponse.cs
export interface ApiResponse<T> {
  success: boolean;
  message?: string;
  data?: T;
  errors?: string[];
}
