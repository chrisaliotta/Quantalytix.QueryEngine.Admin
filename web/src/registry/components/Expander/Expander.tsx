"use client"

import type React from "react"

import { ChevronDown } from "lucide-react"
import { useState } from "react"
import { cn } from "@/lib/utils"
import "./expander.css"

interface ExpanderProps extends Omit<React.HTMLAttributes<HTMLDivElement>, 'title'> {
  title: string | React.ReactNode
  children: React.ReactNode
  defaultOpen?: boolean
  open?: boolean
  onOpenChange?: (open: boolean) => void
  renderTitle?: (props: { isOpen: boolean; title: string | React.ReactNode }) => React.ReactNode
}

export function Expander({
  title,
  children,
  defaultOpen = true,
  open: controlledOpen,
  onOpenChange,
  className,
  style,
  renderTitle,
  ...props
}: ExpanderProps) {
  const [uncontrolledOpen, setUncontrolledOpen] = useState(defaultOpen)

  // Use controlled state if provided, otherwise use internal state
  const isOpen = controlledOpen !== undefined ? controlledOpen : uncontrolledOpen

  const handleToggle = () => {
    const newOpen = !isOpen
    if (controlledOpen === undefined) {
      setUncontrolledOpen(newOpen)
    }
    onOpenChange?.(newOpen)
  }

  const titleContent = renderTitle ? renderTitle({ isOpen, title }) : title

  return (
    <div
      data-slot="expander"
      className={cn("space-y-2", className)}
      style={{
        "--expander-spacing": "var(--expander-spacing, 0.5rem)",
        "--expander-title-color": "var(--expander-title-color, hsl(var(--muted-foreground)))",
        "--expander-title-hover-color": "var(--expander-title-hover-color, hsl(var(--foreground)))",
        "--expander-icon-color": "var(--expander-icon-color, hsl(var(--muted-foreground)))",
        "--expander-content-spacing": "var(--expander-content-spacing, 0.375rem)",
        "--expander-transition-duration": "var(--expander-transition-duration, 300ms)",
        ...style,
      } as React.CSSProperties}
      {...props}
    >
      <button
        data-slot="expander-trigger"
        onClick={handleToggle}
        className="flex items-center justify-between w-full px-1 py-1 text-xs font-semibold uppercase tracking-wider transition-colors"
        style={{
          color: "var(--expander-title-color)",
        }}
      >
        <span data-slot="expander-title">{titleContent}</span>
        <ChevronDown
          data-slot="expander-icon"
          className={cn(
            "w-3.5 h-3.5 transition-transform ease-in-out",
            isOpen && "rotate-180",
          )}
          style={{
            color: "var(--expander-icon-color)",
            transitionDuration: "var(--expander-transition-duration)",
          }}
        />
      </button>
      <div
        data-slot="expander-content"
        className={cn(
          "grid transition-all ease-in-out",
          isOpen ? "grid-rows-[1fr] opacity-100" : "grid-rows-[0fr] opacity-0",
        )}
        style={{
          transitionDuration: "var(--expander-transition-duration)",
        }}
      >
        <div
          data-slot="expander-content-inner"
          className="min-h-0 overflow-hidden"
          style={{
            paddingTop: "var(--expander-content-spacing)",
          }}
        >
          {children}
        </div>
      </div>
    </div>
  )
}
