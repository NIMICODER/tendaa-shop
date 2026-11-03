import { createBrowserRouter, Navigate } from 'react-router-dom'
import { CatalogPage } from '@/pages/CatalogPage'
import { ConversationsPage } from '@/pages/ConversationsPage'
import { HandoffsPage } from '@/pages/HandoffsPage'
import { LandingPage } from '@/pages/LandingPage'
import { LoginPage } from '@/pages/LoginPage'
import { OrderDetailPage } from '@/pages/OrderDetailPage'
import { OrdersPage } from '@/pages/OrdersPage'
import { SettingsPage } from '@/pages/SettingsPage'
import { Layout } from './Layout'
import { RequireAuth } from './RequireAuth'

export const router = createBrowserRouter([
  { path: '/', element: <LandingPage /> },
  { path: '/login', element: <LoginPage /> },
  {
    element: <RequireAuth />,
    children: [
      {
        element: <Layout />,
        children: [
          { path: 'orders', element: <OrdersPage /> },
          { path: 'orders/:orderId', element: <OrderDetailPage /> },
          { path: 'handoffs', element: <HandoffsPage /> },
          { path: 'conversations', element: <ConversationsPage /> },
          { path: 'catalog', element: <CatalogPage /> },
          { path: 'settings', element: <SettingsPage /> },
        ],
      },
    ],
  },
  { path: '*', element: <Navigate to="/" replace /> },
])
