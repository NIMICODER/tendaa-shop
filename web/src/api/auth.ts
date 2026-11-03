import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import {
  apiFetch,
  fetchAntiforgeryToken,
  resetAntiforgeryToken,
  type TendaaResponse,
} from './client'

export const authKeys = {
  me: ['auth', 'me'] as const,
}

export function useAuthMe() {
  return useQuery({
    queryKey: authKeys.me,
    queryFn: () => apiFetch<TendaaResponse>('/api/auth/me'),
    retry: false,
  })
}

export function useLogin() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (password: string) =>
      apiFetch<TendaaResponse>('/api/auth/login', {
        method: 'POST',
        body: JSON.stringify({ password }),
      }),
    onSuccess: async () => {
      // Antiforgery tokens are identity-bound: the anonymous token can't authorize
      // requests sent as the signed-in seller, so fetch a fresh one before doing more.
      await fetchAntiforgeryToken()
      await queryClient.invalidateQueries({ queryKey: authKeys.me })
    },
  })
}

export function useLogout() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: () => apiFetch<TendaaResponse>('/api/auth/logout', { method: 'POST' }),
    onSuccess: async () => {
      resetAntiforgeryToken()
      await fetchAntiforgeryToken()
      await queryClient.invalidateQueries({ queryKey: authKeys.me })
    },
  })
}
