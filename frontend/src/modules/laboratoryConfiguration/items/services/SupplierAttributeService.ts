import { apiClient } from "../../../../services/apiClient";

export interface SupplierStatedAttributeDto {
  id: number;
  itemId: number;
  grade: string | null;
  name: string;
  statedValue: string;
  sortOrder: number;
}

export interface SupplierStatedAttributeRequest {
  itemId?: number;
  grade?: string | null;
  name: string;
  statedValue: string;
  sortOrder?: number;
}

export const SupplierAttributeService = {
  async listForItem(itemId: number): Promise<SupplierStatedAttributeDto[]> {
    const res = await apiClient.get<{ data: SupplierStatedAttributeDto[] }>(`/items/${itemId}/supplier-attributes`);
    return res.data.data;
  },

  async create(itemId: number, req: SupplierStatedAttributeRequest): Promise<SupplierStatedAttributeDto> {
    const res = await apiClient.post<{ data: SupplierStatedAttributeDto }>(`/items/${itemId}/supplier-attributes`, {
      ...req,
      itemId,
    });
    return res.data.data;
  },

  async update(id: number, req: SupplierStatedAttributeRequest): Promise<SupplierStatedAttributeDto> {
    const res = await apiClient.put<{ data: SupplierStatedAttributeDto }>(`/supplier-attributes/${id}`, req);
    return res.data.data;
  },

  async delete(id: number): Promise<void> {
    await apiClient.delete(`/supplier-attributes/${id}`);
  },
};

export const SupplierStatedAttributeService = SupplierAttributeService;
