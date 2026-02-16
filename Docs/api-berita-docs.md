# API DOKUMENTASI - BERITA ENDPOINTS

## Base URL
```
/api/v1/berita
```

Semua endpoint memerlukan JWT Authentication (kecuali yang ditandai sebagai Anonymous)
Header: `Authorization: Bearer <your-jwt-token>`

---

## 1. GET All Berita (dengan filter & pagination)

### Endpoint
```
GET /api/v1/berita?page=1&pageSize=10
```

### Query Parameters (semua optional)
- `skpdId` (int, optional): Filter berita berdasarkan SKPD ID
- `categoryId` (int, optional): Filter berita berdasarkan Category ID
- `status` (string, optional): Filter berdasarkan status (draft, review, published, archived)
- `page` (int, optional, default: 1): Nomor halaman
- `pageSize` (int, optional, default: 10): Jumlah item per halaman

**Note**: Semua query parameters bersifat optional. Jika tidak disertakan, akan menggunakan nilai default.

### Contoh Request
```bash
# Semua berita (tanpa filter, default page=1 pageSize=10)
GET /api/v1/berita

# Hanya dengan pagination
GET /api/v1/berita?page=2
GET /api/v1/berita?page=1&pageSize=20

# Filter by SKPD
GET /api/v1/berita?skpdId=1

# Filter by category
GET /api/v1/berita?categoryId=5

# Filter by status
GET /api/v1/berita?status=published

# Kombinasi filter + pagination
GET /api/v1/berita?skpdId=1&categoryId=5&status=published&page=2&pageSize=20
```

### Response (200 OK)
```json
[
  {
    "id": 1,
    "skpdId": 1,
    "skpdNama": "Dinas Pendidikan Kabupaten Merauke",
    "categoryId": 5,
    "categoryName": "Berita Umum",
    "title": "Berita Terbaru dari Dinas Pendidikan",
    "slug": "berita-terbaru-dari-dinas-pendidikan",
    "excerpt": "Ringkasan singkat berita...",
    "content": "Konten lengkap berita...",
    "thumbnailUrl": "https://example.com/image.jpg",
    "status": "published",
    "publishedAt": "2026-02-15T10:30:00Z",
    "viewCount": 125,
    "createdBy": 5,
    "createdByName": "admin.disdik",
    "createdAt": "2026-02-14T08:00:00Z",
    "updatedAt": "2026-02-15T10:30:00Z"
  }
]
```

---

## 2. GET Berita by ID

### Endpoint
```
GET /api/v1/berita/{id}
```

### Contoh Request
```bash
GET /api/v1/berita/1
```

### Response (200 OK)
```json
{
  "id": 1,
  "skpdId": 1,
  "skpdNama": "Dinas Pendidikan Kabupaten Merauke",
  "categoryId": 5,
  "categoryName": "Berita Umum",
  "title": "Berita Terbaru dari Dinas Pendidikan",
  "slug": "berita-terbaru-dari-dinas-pendidikan",
  "excerpt": "Ringkasan singkat berita...",
  "content": "Konten lengkap berita...",
  "thumbnailUrl": "https://example.com/image.jpg",
  "status": "published",
  "publishedAt": "2026-02-15T10:30:00Z",
  "viewCount": 125,
  "createdBy": 5,
  "createdByName": "admin.disdik",
  "createdAt": "2026-02-14T08:00:00Z",
  "updatedAt": "2026-02-15T10:30:00Z"
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

## 3. GET Berita by Category Slug (Public)

### Endpoint
```
GET /api/v1/berita/category/{skpdId}/{categorySlug}
```

### Authorization
**Anonymous** - Tidak memerlukan authentication

### Query Parameters (optional)
- `page` (int, optional, default: 1): Nomor halaman
- `pageSize` (int, optional, default: 10): Jumlah item per halaman

**Note**: Kedua parameter bersifat optional. Jika tidak disertakan, akan menggunakan nilai default.

### Path Parameters
- `skpdId` (int, required): ID SKPD
- `categorySlug` (string, required): Slug kategori

### Contoh Request
```bash
# Get berita dari kategori "berita-umum" SKPD 1 (default pagination)
GET /api/v1/berita/category/1/berita-umum

# Dengan custom pagination
GET /api/v1/berita/category/1/berita-umum?page=2
GET /api/v1/berita/category/1/berita-umum?pageSize=20
GET /api/v1/berita/category/1/berita-umum?page=2&pageSize=20

# Contoh kategori lain
GET /api/v1/berita/category/1/pengumuman
GET /api/v1/berita/category/2/agenda-kegiatan
```

### Response (200 OK)
```json
[
  {
    "id": 1,
    "skpdId": 1,
    "skpdNama": "Dinas Pendidikan Kabupaten Merauke",
    "categoryId": 5,
    "categoryName": "Berita Umum",
    "title": "Berita Terbaru dari Dinas Pendidikan",
    "slug": "berita-terbaru-dari-dinas-pendidikan",
    "excerpt": "Ringkasan singkat berita...",
    "content": "Konten lengkap berita...",
    "thumbnailUrl": "https://example.com/image.jpg",
    "status": "published",
    "publishedAt": "2026-02-15T10:30:00Z",
    "viewCount": 125,
    "createdBy": 5,
    "createdByName": "admin.disdik",
    "createdAt": "2026-02-14T08:00:00Z",
    "updatedAt": "2026-02-15T10:30:00Z"
  }
]
```

### Notes
- Endpoint ini hanya mengembalikan berita dengan status **"published"**
- Hasil diurutkan berdasarkan `published_at` DESC (terbaru dulu)
- Kategori harus cocok dengan `skpd_id` yang diberikan (categories.skpd_id = skpdId)
- Jika kategori tidak ditemukan atau tidak ada berita, akan return array kosong `[]`

### Use Case
Endpoint ini ideal untuk:
- Public website yang ingin menampilkan berita per kategori
- Frontend yang butuh filter kategori tanpa authentication
- RSS feed atau sitemap generation
- Mobile app public view

---

## 4. POST Create Berita

### Endpoint
```
POST /api/v1/berita
```

### Headers Required
```
Authorization: Bearer <your-jwt-token>
Content-Type: application/json
```

### Request Body
```json
{
  "skpdId": 1,
  "categoryId": 5,
  "title": "Judul Berita Baru",
  "slug": "judul-berita-baru",
  "excerpt": "Ringkasan singkat berita ini",
  "content": "Konten lengkap dari berita yang dibuat...",
  "thumbnailUrl": "https://example.com/thumbnail.jpg",
  "status": "draft"
}
```

### Field Notes
- `categoryId` (optional): ID kategori, bisa null
- `status` options: draft, review, published, archived

### Field Status Options
- `draft` (default): Berita masih dalam draft
- `review`: Berita sedang dalam review
- `published`: Berita dipublikasikan (publishedAt akan otomatis di-set)
- `archived`: Berita diarsipkan

### Response (201 Created)
```json
{
  "id": 2,
  "skpdId": 1,
  "skpdNama": "Dinas Pendidikan Kabupaten Merauke",
  "categoryId": 5,
  "categoryName": "Berita Umum",
  "title": "Judul Berita Baru",
  "slug": "judul-berita-baru",
  "excerpt": "Ringkasan singkat berita ini",
  "content": "Konten lengkap dari berita yang dibuat...",
  "thumbnailUrl": "https://example.com/thumbnail.jpg",
  "status": "draft",
  "publishedAt": null,
  "viewCount": 0,
  "createdBy": 5,
  "createdByName": "admin.disdik",
  "createdAt": "2026-02-16T07:00:00Z",
  "updatedAt": null
}
```

### Notes
- `createdBy` diambil otomatis dari JWT token user yang login
- Jika status = "published", maka `publishedAt` akan di-set ke waktu sekarang
- `slug` harus unique per SKPD (kombinasi skpd_id + slug harus unik)

---

## 5. PUT Update Berita

### Endpoint
```
PUT /api/v1/berita/{id}
```

### Authorization Rules
- **Admin**: Dapat update semua berita
- **User biasa**: Hanya dapat update berita miliknya sendiri (createdBy = userId)

### Headers Required
```
Authorization: Bearer <your-jwt-token>
Content-Type: application/json
```

### Request Body
```json
{
  "categoryId": 5,
  "title": "Judul Berita yang Diupdate",
  "slug": "judul-berita-yang-diupdate",
  "excerpt": "Ringkasan yang diupdate",
  "content": "Konten yang diupdate...",
  "thumbnailUrl": "https://example.com/new-thumbnail.jpg",
  "status": "published"
}
```

### Response (204 No Content)
Body kosong jika berhasil

### Response (404 Not Found)
Jika berita tidak ditemukan

### Response (403 Forbidden)
Jika user biasa mencoba update berita orang lain

### Notes
- Jika status berubah dari non-published ke "published", `publishedAt` akan di-set otomatis
- `updatedAt` akan di-update otomatis

---

## 6. DELETE Berita (Soft Delete)

### Endpoint
```
DELETE /api/v1/berita/{id}
```

### Authorization
**Hanya Admin** yang dapat menghapus berita

### Headers Required
```
Authorization: Bearer <your-jwt-token>
```

### Contoh Request
```bash
DELETE /api/v1/berita/1
```

### Response (204 No Content)
Body kosong jika berhasil

### Response (404 Not Found)
Jika berita tidak ditemukan

### Response (403 Forbidden)
Jika user bukan Admin

### Notes
- Ini adalah soft delete, berita tidak benar-benar dihapus dari database
- `deleted_at` akan di-set ke waktu sekarang

---

## 7. POST Increment View Count

### Endpoint
```
POST /api/v1/berita/{id}/view
```

### Authorization
**Anonymous** - Tidak memerlukan authentication

### Contoh Request
```bash
POST /api/v1/berita/1/view
```

### Response (204 No Content)
Body kosong jika berhasil

### Response (404 Not Found)
Jika berita tidak ditemukan

### Use Case
Endpoint ini digunakan untuk tracking jumlah views/pembaca berita.
Biasanya dipanggil dari frontend saat user membuka/membaca berita.

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

---

## Status Workflow Berita

```
draft → review → published → archived
  ↑                            ↓
  └────────────────────────────┘
```

- **draft**: Berita baru dibuat, masih dalam pengeditan
- **review**: Berita sudah selesai diedit, menunggu review
- **published**: Berita sudah direview dan dipublikasikan (publishedAt terisi)
- **archived**: Berita lama yang diarsipkan

---

## Contoh Penggunaan dengan cURL

### Create Berita
```bash
curl -X POST https://api.example.com/api/v1/berita \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." \
  -H "Content-Type: application/json" \
  -d '{
    "skpdId": 1,
    "title": "Berita Penting",
    "slug": "berita-penting",
    "excerpt": "Ini berita penting",
    "content": "Konten lengkap...",
    "status": "draft"
  }'
```

### Update Berita
```bash
curl -X PUT https://api.example.com/api/v1/berita/1 \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Berita Penting (Updated)",
    "slug": "berita-penting-updated",
    "excerpt": "Update ringkasan",
    "content": "Update konten...",
    "status": "published"
  }'
```

### Get Berita by SKPD
```bash
curl -X GET "https://api.example.com/api/v1/berita?skpdId=1&status=published" \
  -H "Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
```

### Increment View
```bash
curl -X POST https://api.example.com/api/v1/berita/1/view
```
