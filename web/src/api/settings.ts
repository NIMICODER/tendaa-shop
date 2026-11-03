import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiFetch, type TendaaResponse, type TendaaResult } from './client'

export interface ShopSettings {
  shopId: string
  name: string
  country: string
  currency: string
  timeZone: string
  hoursText: string | null
  rulesText: string | null
  bankName: string | null
  bankAccountNumber: string | null
  bankAccountName: string | null
}

export interface ShopSettingsInput {
  name: string
  country: string
  currency: string
  timeZone: string
  hoursText: string | null
  rulesText: string | null
  bankName: string | null
  bankAccountNumber: string | null
  bankAccountName: string | null
}

export interface DeliveryZoneSummary {
  id: string
  name: string
  fee: number
  deliveryTimeText: string
  formattedFee: string
}

export interface DeliveryZoneInput {
  name: string
  fee: number
  deliveryTimeText: string | null
}

export function useSettings() {
  return useQuery({
    queryKey: ['settings'],
    queryFn: async () => {
      const result = await apiFetch<TendaaResult<ShopSettings>>('/api/settings')
      if (!result.isSuccessful) {
        throw new Error(result.message ?? 'Could not load settings.')
      }
      return result.data
    },
  })
}

export function useUpdateSettings() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: ShopSettingsInput) =>
      apiFetch<TendaaResult<ShopSettings>>('/api/settings', {
        method: 'PUT',
        body: JSON.stringify(input),
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['settings'] })
    },
  })
}

export function useZones() {
  return useQuery({
    queryKey: ['zones'],
    queryFn: async () => {
      const result = await apiFetch<TendaaResult<DeliveryZoneSummary[]>>(
        '/api/settings/delivery-zones',
      )
      if (!result.isSuccessful) {
        throw new Error(result.message ?? 'Could not load delivery zones.')
      }
      return result.data
    },
  })
}

export function useCreateZone() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: DeliveryZoneInput) =>
      apiFetch<TendaaResult<DeliveryZoneSummary>>('/api/settings/delivery-zones', {
        method: 'POST',
        body: JSON.stringify(input),
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['zones'] })
    },
  })
}

export function useUpdateZone() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ zoneId, input }: { zoneId: string; input: DeliveryZoneInput }) =>
      apiFetch<TendaaResult<DeliveryZoneSummary>>(`/api/settings/delivery-zones/${zoneId}`, {
        method: 'PUT',
        body: JSON.stringify(input),
      }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['zones'] })
    },
  })
}

export function useDeleteZone() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (zoneId: string) =>
      apiFetch<TendaaResponse>(`/api/settings/delivery-zones/${zoneId}`, { method: 'DELETE' }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['zones'] })
    },
  })
}
