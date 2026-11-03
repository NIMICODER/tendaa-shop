export function PlaceholderPage({ title, step }: { title: string; step: string }) {
  return (
    <div className="p-8">
      <h1 className="text-2xl font-bold">{title}</h1>
      <p className="mt-2 text-sm text-muted-foreground">Arrives in {step}.</p>
    </div>
  )
}
