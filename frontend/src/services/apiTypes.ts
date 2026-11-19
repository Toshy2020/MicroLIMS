export interface FieldError {
  field: string;
  message: string;
  row?: number | null;
}

export interface ApiResponse<T> {
  success: boolean;
  message?: string | null;
  data?: T | null;
  errors?: string[] | null;
  fieldErrors?: FieldError[] | null;
}
