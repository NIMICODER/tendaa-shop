import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiFetch, type TendaaResponse, type TendaaResult } from './client'

export interface VariantDashboardItem {
  variantId: string
  size: string | null
  colour: string | null
  stock: number
  effectivePrice: number
  formattedEffectivePrice: string
}

export interface ProductDashboardItem {
  productId: string
  name: string
  category: string | null
  description: string | null
  price: number
  formattedPrice: string
  photoUrl: string | null
  variants: VariantDashboardItem[]
}

export interface VariantInput {
  variantId: string | null
  size: string | null
  colour: string | null
  stock: number
  priceOverride: number | null
}

export interface ProductInput {
  name: string
  category: string | null
  description: string | null
  price: number
  photoUrl: string | null
  variants: VariantInput[]
}

export const catalogKeys = {
  all: ['catalog'] as const,
}

export function useProducts() {
  return useQuery({
    queryKey: catalogKeys.all,
    queryFn: async () => {
      const result = await apiFetch<TendaaResult<ProductDashboardItem[]>>('/api/products')
      if (!result.isSuccessful) {
        throw new Error(result.message ?? 'Could not load the catalog.')
      }
      return result.data
    },
  })
}

export function useCreateProduct() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: ProductInput) =>
      apiFetch<TendaaResult<ProductDashboardItem>>('/api/products', {
        method: 'POST',
        body: JSON.stringify(input),
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: catalogKeys.all })
    },
  })
}

export function useUpdateProduct() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ productId, input }: { productId: string; input: ProductInput }) =>
      apiFetch<TendaaResult<ProductDashboardItem>>(`/api/products/${productId}`, {
        method: 'PUT',
        body: JSON.stringify(input),
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: catalogKeys.all })
    },
  })
}

export function useDeleteProduct() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (productId: string) =>
      apiFetch<TendaaResponse>(`/api/products/${productId}`, { method: 'DELETE' }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: catalogKeys.all })
    },
  })
}
