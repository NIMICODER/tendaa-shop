import { useState } from 'react'
import { Pencil, Plus, Trash2 } from 'lucide-react'
import {
  useCreateProduct,
  useDeleteProduct,
  useProducts,
  useUpdateProduct,
  type ProductDashboardItem,
  type ProductInput,
  type VariantInput,
} from '@/api/catalog'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Modal } from '@/components/ui/modal'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table'
import { Textarea } from '@/components/ui/textarea'

interface VariantDraft {
  key: string
  variantId: string | null
  size: string
  colour: string
  stock: string
  priceOverride: string
}

const newVariantDraft = (variantId: string | null = null): VariantDraft => ({
  key: `${Date.now()}-${Math.random().toString(36).slice(2)}`,
  variantId,
  size: '',
  colour: '',
  stock: '',
  priceOverride: '',
})

export function CatalogPage() {
  const products = useProducts()
  const [editing, setEditing] = useState<ProductDashboardItem | 'new' | null>(null)

  return (
    <div className="p-8">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Catalog</h1>
        <Button onClick={() => setEditing('new')}>
          <Plus /> New product
        </Button>
      </div>

      <Card className="mt-6 overflow-hidden">
        {products.isPending ? (
          <p className="p-8 text-sm text-muted-foreground">Loading catalog…</p>
        ) : products.isError ? (
          <div className="flex flex-col items-start gap-3 p-8">
            <p className="text-sm text-muted-foreground">
              Couldn&apos;t load the catalog: {products.error.message}
            </p>
            <Button variant="outline" size="sm" onClick={() => void products.refetch()}>
              Retry
            </Button>
          </div>
        ) : products.data.length === 0 ? (
          <p className="p-8 text-sm text-muted-foreground">No products yet.</p>
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>Product</TableHead>
                <TableHead>Category</TableHead>
                <TableHead>Price</TableHead>
                <TableHead>Variants</TableHead>
                <TableHead>Total stock</TableHead>
                <TableHead />
              </TableRow>
            </TableHeader>
            <TableBody>
              {products.data.map((p) => (
                <TableRow key={p.productId}>
                  <TableCell>
                    <div className="flex items-center gap-3">
                      {p.photoUrl ? (
                        <img
                          src={p.photoUrl}
                          alt=""
                          className="size-9 rounded-lg border border-border object-cover"
                        />
                      ) : (
                        <div className="size-9 rounded-lg bg-muted" />
                      )}
                      <span className="font-semibold">{p.name}</span>
                    </div>
                  </TableCell>
                  <TableCell>{p.category ?? '—'}</TableCell>
                  <TableCell className="font-medium">{p.formattedPrice}</TableCell>
                  <TableCell>{p.variants.length}</TableCell>
                  <TableCell>{p.variants.reduce((sum, v) => sum + v.stock, 0)}</TableCell>
                  <TableCell className="text-right">
                    <button
                      type="button"
                      title="Edit"
                      onClick={() => setEditing(p)}
                      className="inline-flex h-8 w-8 items-center justify-center rounded-lg text-muted-foreground hover:bg-muted"
                    >
                      <Pencil className="size-4" />
                    </button>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </Card>

      {editing && (
        <ProductModal
          product={editing === 'new' ? null : editing}
          onClose={() => setEditing(null)}
        />
      )}
    </div>
  )
}

function ProductModal({
  product,
  onClose,
}: {
  product: ProductDashboardItem | null
  onClose: () => void
}) {
  const create = useCreateProduct()
  const update = useUpdateProduct()
  const remove = useDeleteProduct()
  const [name, setName] = useState(product?.name ?? '')
  const [category, setCategory] = useState(product?.category ?? '')
  const [description, setDescription] = useState(product?.description ?? '')
  const [price, setPrice] = useState(product ? String(product.price) : '')
  const [photoUrl, setPhotoUrl] = useState(product?.photoUrl ?? '')
  const [variants, setVariants] = useState<VariantDraft[]>(() =>
    product
      ? product.variants.map((v) => ({
          key: v.variantId,
          variantId: v.variantId,
          size: v.size ?? '',
          colour: v.colour ?? '',
          stock: String(v.stock),
          priceOverride: '',
        }))
      : [newVariantDraft()],
  )

  const saving = create.isPending || update.isPending
  const error = create.error?.message ?? update.error?.message ?? remove.error?.message ?? null

  function setVariant(key: string, patch: Partial<VariantDraft>) {
    setVariants((rows) => rows.map((r) => (r.key === key ? { ...r, ...patch } : r)))
  }

  function toInput(): ProductInput | null {
    const parsedPrice = Number(price)
    if (name.trim().length === 0 || !Number.isFinite(parsedPrice) || parsedPrice < 0) {
      return null
    }
    const parsed: VariantInput[] = []
    for (const v of variants) {
      const stock = v.stock.trim() === '' ? NaN : Number(v.stock)
      const overrideText = v.priceOverride.trim()
      const priceOverride = overrideText === '' ? null : Number(overrideText)
      if (
        !Number.isInteger(stock) ||
        stock < 0 ||
        (priceOverride !== null && (!Number.isFinite(priceOverride) || priceOverride < 0))
      ) {
        return null
      }
      parsed.push({
        variantId: v.variantId,
        size: v.size.trim() === '' ? null : v.size.trim(),
        colour: v.colour.trim() === '' ? null : v.colour.trim(),
        stock,
        priceOverride,
      })
    }
    return {
      name: name.trim(),
      category: category.trim() === '' ? null : category.trim(),
      description: description.trim() === '' ? null : description.trim(),
      price: parsedPrice,
      photoUrl: photoUrl.trim() === '' ? null : photoUrl.trim(),
      variants: parsed,
    }
  }

  function save() {
    const input = toInput()
    if (!input || saving) {
      return
    }
    if (product) {
      update.mutate(
        { productId: product.productId, input },
        { onSuccess: (result) => result.isSuccessful && onClose() },
      )
    } else {
      create.mutate(input, { onSuccess: (result) => result.isSuccessful && onClose() })
    }
  }

  function destroy() {
    if (!product || remove.isPending) {
      return
    }
    if (window.confirm(`Delete "${product.name}" and all its variants?`)) {
      remove.mutate(product.productId, {
        onSuccess: (result) => result.isSuccessful && onClose(),
      })
    }
  }

  return (
    <Modal title={product ? 'Edit product' : 'New product'} onClose={onClose}>
      <div className="flex flex-col gap-3">
        <Field label="Name">
          <Input value={name} onChange={(e) => setName(e.target.value)} />
        </Field>
        <div className="grid grid-cols-2 gap-3">
          <Field label="Category">
            <Input value={category} onChange={(e) => setCategory(e.target.value)} />
          </Field>
          <Field label="Price">
            <Input
              value={price}
              inputMode="decimal"
              placeholder="25000"
              onChange={(e) => setPrice(e.target.value)}
            />
          </Field>
        </div>
        <Field label="Description">
          <Textarea value={description} onChange={(e) => setDescription(e.target.value)} />
        </Field>
        <Field label="Photo URL">
          <Input value={photoUrl} onChange={(e) => setPhotoUrl(e.target.value)} />
        </Field>

        <div className="flex flex-col gap-2">
          <p className="text-sm font-medium">Variants</p>
          {variants.map((v) => (
            <div key={v.key} className="grid grid-cols-[1fr_1fr_80px_1fr_auto] items-center gap-2">
              <Input
                placeholder="Size"
                value={v.size}
                onChange={(e) => setVariant(v.key, { size: e.target.value })}
              />
              <Input
                placeholder="Colour"
                value={v.colour}
                onChange={(e) => setVariant(v.key, { colour: e.target.value })}
              />
              <Input
                placeholder="Stock"
                inputMode="numeric"
                value={v.stock}
                onChange={(e) => setVariant(v.key, { stock: e.target.value })}
              />
              <Input
                placeholder="Price override"
                inputMode="decimal"
                value={v.priceOverride}
                onChange={(e) => setVariant(v.key, { priceOverride: e.target.value })}
              />
              <button
                type="button"
                title="Remove variant"
                onClick={() => setVariants((rows) => rows.filter((r) => r.key !== v.key))}
                className="flex h-8 w-8 items-center justify-center rounded-lg text-muted-foreground hover:bg-muted"
              >
                <Trash2 className="size-4" />
              </button>
            </div>
          ))}
          <Button variant="outline" size="sm" onClick={() => setVariants((rows) => [...rows, newVariantDraft()])}>
            <Plus /> Add variant
          </Button>
        </div>

        {error && <p className="text-sm font-medium text-destructive">{error}</p>}

        <div className="mt-1 flex items-center justify-between">
          <div>
            {product && (
              <Button variant="ghost" onClick={destroy} disabled={remove.isPending}>
                <Trash2 /> {remove.isPending ? 'Deleting…' : 'Delete'}
              </Button>
            )}
          </div>
          <div className="flex gap-2">
            <Button variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button onClick={save} disabled={saving}>
              {saving ? 'Saving…' : 'Save'}
            </Button>
          </div>
        </div>
      </div>
    </Modal>
  )
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="flex flex-col gap-1.5 text-sm font-medium">
      {label}
      {children}
    </label>
  )
}
