import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { useQueryClient } from '@tanstack/react-query'
import { useEffect, type ReactNode } from 'react'

export function SignalRProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient()

  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/dashboard')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()

    // Coarse-grained by design: the hub only says "something changed", pages refetch.
    connection.on('OrdersChanged', () => {
      void queryClient.invalidateQueries({ queryKey: ['orders'] })
    })
    connection.on('HandoffsChanged', () => {
      void queryClient.invalidateQueries({ queryKey: ['handoffs'] })
    })

    connection.start().catch(() => {
      // Stay silent: pages poll as the fallback when the hub is unreachable.
    })

    return () => {
      void connection.stop()
    }
  }, [queryClient])

  return children
}
