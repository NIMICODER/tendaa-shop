import * as React from 'react'
import { cva, type VariantProps } from 'class-variance-authority'
import { cn } from '@/lib/utils'

// Colored-dot + label, not a filled pill — matches the dashboard reference studied for Phase 5
// (status cells read as a small dot plus plain-colored text, not a solid chip).
const badgeVariants = cva('inline-flex items-center gap-1.5 whitespace-nowrap text-xs font-medium', {
  variants: {
    variant: {
      default: 'text-accent-foreground',
      success: 'text-status-positive',
      warning: 'text-status-pending',
      danger: 'text-status-negative',
      muted: 'text-status-neutral',
    },
  },
  defaultVariants: {
    variant: 'default',
  },
})

const dotVariants = cva('size-1.5 shrink-0 rounded-full', {
  variants: {
    variant: {
      default: 'bg-primary',
      success: 'bg-status-positive',
      warning: 'bg-status-pending',
      danger: 'bg-status-negative',
      muted: 'bg-status-neutral',
    },
  },
  defaultVariants: {
    variant: 'default',
  },
})

export interface BadgeProps
  extends React.HTMLAttributes<HTMLSpanElement>,
    VariantProps<typeof badgeVariants> {
  dot?: boolean
}

function Badge({ className, variant, dot = true, children, ...props }: BadgeProps) {
  return (
    <span className={cn(badgeVariants({ variant }), className)} {...props}>
      {dot && <span className={cn(dotVariants({ variant }))} aria-hidden="true" />}
      {children}
    </span>
  )
}

export { Badge, badgeVariants }
