# API DOKUMENTASI - CATEGORIES ENDPOINTS

## Base URL
```
/api/v1/categories
```

---

## 1. GET All Categories (dengan filter & pagination)

### Endpoint
```
GET /api/v1/categories
```

### Authorization
**Required** - JWT Token

### Query Parameters (semua optional)
- `skpdId` (int, optional): Filter kategori berdasarkan SKPD ID
- `page` (int, optional, default: 1): Nomor halaman
- `pageSize` (int, optional, default: 10): Jumlah item per halaman

**Note**: Semua query parameters bersifat optional. Jika tidak disertakan, akan menggunakan nilai default.

### Contoh Request
```bash
# Semua kategori (tanpa filter, default page=1 pageSize=10)
GET /api/v1/categories

# Hanya dengan pagination
GET /api/v1/categories?page=2
GET /api/v1/categories?page=1&pageSize=20

# Filter by SKPD
GET /api/v1/categories?skpdId=1

# Kombinasi filter + pagination
GET /api/v1/categories?skpdId=1&page=2&pageSize=20
```

### Response (200 OK)
```json
[
  {
    "id": 1,
    "skpdId": 1,
    "skpdNama": "Dinas Pendidikan Kabupaten Merauke",
    "name": "Berita Umum",
    "slug": "berita-umum",
    "createdAt": "2026-02-14T08:00:00Z"
  },
  {
    "id": 2,
    "skpdId": 1,
    "skpdNama": "Dinas Pendidikan Kabupaten Merauke",
    "name": "Pengumuman",
    "slug": "pengumuman",
    "createdAt": "2026-02-14T08:15:00Z"
  }
]
```

---

## 2. GET Category by ID

### Endpoint
```
GET /api/v1/categories/{id}
```

### Authorization
**Required** - JWT Token

### Path Parameters
- `id` (int, required): ID kategori

### Contoh Request
```bash
GET /api/v1/categories/1
```

### Response (200 OK)
```json
{
  "id": 1,
  "skpdId": 1,
  "skpdNama": "Dinas Pendidikan Kabupaten Merauke",
  "name": "Berita Umum",
  "slug": "berita-umum",
  "createdAt": "2026-02-14T08:00:00Z"
}
```

### Response (404 Not Found)
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Not Found",
  "status": 404
}
```

---

## 3. GET Categories by SKPD ID (Public)

### Endpoint
```
GET /api/v1/categories/skpd/{skpdId}
```

### Authorization
**Anonymous** - Tidak memerlukan authentication

### Path Parameters
- `skpdId` (int, required): ID SKPD

### Contoh Request
```bash
# Get semua kategori dari SKPD 1
GET /api/v1/categories/skpd/1

# Get semua kategori dari SKPD 2
GET /api/v1/categories/skpd/2
```

### Response (200 OK)
```json
[
  {
    "id": 1,
    "skpdId": 1,
    "skpdNama": "Dinas Pendidikan Kabupaten Merauke",
    "name": "Berita Umum",
    "slug": "berita-umum",
    "createdAt": "2026-02-14T08:00:00Z"
  },
  {
    "id": 2,
    "skpdId": 1,
    "skpdNama": "Dinas Pendidikan Kabupaten Merauke",
    "name": "Pengumuman",
    "slug": "pengumuman",
    "createdAt": "2026-02-14T08:15:00Z"
  },
  {
    "id": 3,
    "skpdId": 1,
    "skpdNama": "Dinas Pendidikan Kabupaten Merauke",
    "name": "Agenda Kegiatan",
    "slug": "agenda-kegiatan",
    "createdAt": "2026-02-14T08:30:00Z"
  }
]
```

### Notes
- Endpoint ini tidak memerlukan authentication (public access)
- Hasil diurutkan berdasarkan `name` ASC (A-Z)
- Tidak ada pagination - mengembalikan semua kategori dari SKPD tersebut
- Jika SKPD tidak memiliki kategori, akan return array kosong `[]`

### Use Case
Endpoint ini ideal untuk:
- Public website yang ingin menampilkan daftar kategori berita
- Dropdown/select options di form create/edit berita
- Navigation menu kategori di frontend
- Mobile app public view

---

## 4. POST Create Category

### Endpoint
```
POST /api/v1/categories
```

### Authorization
**Admin Only** - JWT Token dengan role Admin

### Headers Required
```
Authorization: Bearer <your-jwt-token>
Content-Type: application/json
```

### Request Body
```json
{
  "skpdId": 1,
  "name": "Berita Umum",
  "slug": "berita-umum"
}
```

### Field Notes
- `skpdId` (required): ID SKPD yang memiliki kategori ini
- `name` (required): Nama kategori
- `slug` (required): URL-friendly slug (harus lowercase, tanpa spasi)
- **IMPORTANT**: Kombinasi `skpdId` + `slug` harus unique (constraint di database)

### Response (201 Created)
```json
{
  "id": 5,
  "skpdId": 1,
  "skpdNama": "Dinas Pendidikan Kabupaten Merauke",
  "name": "Berita Umum",
  "slug": "berita-umum",
  "createdAt": "2026-02-16T07:00:00Z"
}
```

### Response (409 Conflict)
Jika kombinasi skpdId + slug sudah ada:
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.8",
  "title": "Conflict",
  "status": 409,
  "detail": "Duplicate entry for skpd_id and slug"
}
```

### Notes
- `createdAt` di-set otomatis oleh database
- Slug harus unique per SKPD (tidak boleh ada 2 kategori dengan slug sama di 1 SKPD)
- Slug yang baik: `berita-umum`, `pengumuman`, `agenda-kegiatan`
- Hindari slug: `Berita Umum`, `berita umum`, `Berita_Umum`

---

## 5. PUT Update Category

### Endpoint
```
PUT /api/v1/categories/{id}
```

### Authorization
**Admin Only** - JWT Token dengan role Admin

### Headers Required
```
Authorization: Bearer <your-jwt-token>
Content-Type: application/json
```

### Path Parameters
- `id` (int, required): ID kategori yang akan diupdate

### Request Body
```json
{
  "name": "Berita Umum (Updated)",
  "slug": "berita-umum-updated"
}
```

### Field Notes
- `name` (required): Nama kategori baru
- `slug` (required): Slug baru
- **Note**: `skpdId` tidak dapat diubah (tidak ada di request body)
- Slug baru tetap harus unique untuk SKPD yang sama

### Response (204 No Content)
Body kosong jika berhasil

### Response (404 Not Found)
Jika kategori tidak ditemukan

### Response (403 Forbidden)
Jika user bukan Admin

### Response (409 Conflict)
Jika slug baru sudah digunakan oleh kategori lain di SKPD yang sama

---

## 6. DELETE Category (Hard Delete)

### Endpoint
```
DELETE /api/v1/categories/{id}
```

### Authorization
**Admin Only** - JWT Token dengan role Admin

### Headers Required
```
Authorization: Bearer <your-jwt-token>
```

### Path Parameters
- `id` (int, required): ID kategori yang akan dihapus

### Contoh Request
```bash
DELETE /api/v1/categories/1
```

### Response (204 No Content)
Body kosong jika berhasil

### Response (404 Not Found)
Jika kategori tidak ditemukan

### Response (403 Forbidden)
Jika user bukan Admin

### Notes
- Ini adalah **hard delete** - kategori benar-benar dihapus dari database
- Tidak ada soft delete pada tabel categories
- ⚠️ **WARNING**: Jika ada berita yang menggunakan kategori ini:
  - `berita.category_id` akan di-set menjadi `NULL` (karena constraint `ON DELETE SET NULL`)
  - Berita tidak akan terhapus, hanya kehilangan referensi kategori

---

## Error Responses

### 401 Unauthorized
```json
{
  "type": "https://tools.ietf.org/html/rfc7235#section-3.1",
  "title": "Unauthorized",
  "status": 401
}
```

### 403 Forbidden
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.3",
  "title": "Forbidden",
  "status": 403
}
```

### 404 Not Found
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.4",
  "title": "Not Found",
  "status": 404
}
```

### 409 Conflict
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.8",
  "title": "Conflict",
  "status": 409,
  "detail": "Duplicate entry '1-berita-umum' for key 'skpd_id'"
}
```

---

## Contoh Penggunaan dengan cURL

### Create Category
```bash
curl -X POST https://api.example.com/api/v1/categories \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." \
  -H "Content-Type: application/json" \
  -d '{
    "skpdId": 1,
    "name": "Berita Umum",
    "slug": "berita-umum"
  }'
```

### Update Category
```bash
curl -X PUT https://api.example.com/api/v1/categories/1 \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Berita Umum (Updated)",
    "slug": "berita-umum-updated"
  }'
```

### Get Categories by SKPD (Public)
```bash
curl -X GET https://api.example.com/api/v1/categories/skpd/1
```

### Delete Category
```bash
curl -X DELETE https://api.example.com/api/v1/categories/1 \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
```

---

## Best Practices

### 1. Slug Naming Convention
**Good slugs**:
- `berita-umum`
- `pengumuman`
- `agenda-kegiatan`
- `info-publik`
- `layanan-online`

**Bad slugs**:
- `Berita Umum` (tidak lowercase)
- `berita umum` (ada spasi)
- `Berita_Umum` (gunakan dash, bukan underscore)
- `berita-umum-123` (hindari angka kecuali perlu)

### 2. Category Organization
Setiap SKPD sebaiknya memiliki kategori standar:
- Berita Umum
- Pengumuman
- Agenda Kegiatan
- Layanan Publik
- Info Regulasi

### 3. Managing Categories
- Buat kategori sebelum membuat berita
- Gunakan nama yang deskriptif dan mudah dipahami
- Konsisten dalam penamaan antar SKPD
- Hindari membuat terlalu banyak kategori (ideal: 5-10 per SKPD)

### 4. Deletion Safety
Sebelum delete kategori:
1. Check apakah ada berita yang menggunakan kategori tersebut
2. Move berita ke kategori lain jika perlu
3. Atau terima bahwa berita akan kehilangan kategori (set to NULL)

---

## Integration with Berita API

### Workflow: Create Berita with Category

1. **Get available categories**:
```bash
GET /api/v1/categories/skpd/1
```

2. **Create berita with category**:
```bash
POST /api/v1/berita
{
  "skpdId": 1,
  "categoryId": 5,  // <- dari response step 1
  "title": "...",
  ...
}
```

### Workflow: Display Categories Menu

```javascript
// Frontend code example
async function loadCategoriesMenu(skpdId) {
  const response = await fetch(`/api/v1/categories/skpd/${skpdId}`);
  const categories = await response.json();
  
  // Render as navigation menu
  categories.forEach(cat => {
    renderMenuItem(cat.name, `/berita/category/${skpdId}/${cat.slug}`);
  });
}
```

---

## Testing Checklist

- [ ] Test create category dengan berbagai slug formats
- [ ] Test create category dengan duplicate slug (should fail)
- [ ] Test create category dengan skpdId yang tidak valid
- [ ] Test update category slug
- [ ] Test update category ke slug yang sudah ada (should fail)
- [ ] Test delete category yang tidak memiliki berita
- [ ] Test delete category yang memiliki berita (check berita.category_id becomes NULL)
- [ ] Test get categories by non-existent SKPD (should return empty array)
- [ ] Test get all dengan filter skpdId
- [ ] Test get all dengan pagination
- [ ] Test authorization: non-admin trying to create/update/delete (should fail)
- [ ] Test public access to GET /skpd/{skpdId} (should work without token)

---

**API Version**: v1.0.0  
**Last Updated**: 2026-02-16  
**Related APIs**: Berita API
