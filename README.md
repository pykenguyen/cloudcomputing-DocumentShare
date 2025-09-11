 Nền tảng chia sẻ tài liệu cho sinh viên & giảng viên: **tải lên → duyệt → tìm kiếm → xem trước → bình luận → báo cáo vi phạm**.  
> Xây dựng bằng **ASP.NET Core MVC + SQL Server + Entity Framework Core**.

---

## 🗂 Mục lục
- [Tính năng](#-tính-năng)
- [Công nghệ](#-công-nghệ)
- [Kiến trúc & Bảo mật](#-kiến-trúc--bảo-mật)
- [Bắt đầu nhanh](#-bắt-đầu-nhanh)
- [Cấu trúc thư mục](#-cấu-trúc-thư-mục)
- [Ảnh minh họa](#-ảnh-minh-họa)
- [Roadmap](#-roadmap)
- [Đóng góp](#-đóng-góp)
- [Giấy phép](#-giấy-phép)
- [English Summary](#-english-summary)

---

## ✨ Tính năng
- **Upload tài liệu**: PDF/DOCX/ZIP… kèm **tiêu đề, mô tả, danh mục, tags**.
- **Xem trước PDF** ngay trên web (PDF viewer).
- **Tìm kiếm & lọc** theo tên, danh mục, tác giả, thời gian.
- **Like / Bookmark**, **đếm lượt xem/tải**.
- **Bình luận**, **báo cáo vi phạm**.
- **Quy trình duyệt**: tài liệu mới → **Admin** duyệt → xuất bản.
- **Trang quản trị**: ẩn/xóa, duyệt, thống kê gọn nhẹ.
- **Bảo mật file**: tải qua endpoint có kiểm tra quyền, **không lộ đường dẫn vật lý**.
- **.gitignore** & **User Secrets** để không commit secrets/đồ build.

---

## 🛠 Công nghệ
- **Backend**: ASP.NET Core MVC (.NET), **Entity Framework Core**, LINQ.
- **Database**: **SQL Server** (`DocumentSharingDB`) – script: `DocumentSharingDB.sql`.
- **UI**: Razor Views, Bootstrap; có thể gắn PDF.js cho preview.
- **Triển khai**: Windows/IIS; sẵn sàng Docker hoá.

---

## 🧭 Kiến trúc & Bảo mật
- **Layering**: Controllers → Services/DbContext → SQL Server.
- **Thực thể chính**: `Users`, `Documents`, `Categories`, `Comments`, `Reports`, `Transactions` (lượt tải/like).  
- **Quyền**: `User`, `Admin`. Upload ở trạng thái **Pending** cho đến khi **Admin** duyệt.
- **Bảo mật**:
  - Không trả đường dẫn thật, luôn thông qua controller action.
  - Secrets (chuỗi kết nối, API key) dùng **User Secrets/ENV**, **không commit** vào repo.
  - `.gitignore` loại trừ `.vs/`, `bin/`, `obj/`, `*.pubxml`, `*.user`, `appsettings.*.local.json`, `node_modules`, `dist`.

---

## 🚀 Bắt đầu nhanh

### 1) Yêu cầu
- Visual Studio 2022 + .NET SDK phù hợp
- SQL Server 2019/2022 + SSMS

### 2) Tạo Database
**Cách A – dùng script**  
Mở `DocumentSharingDB.sql` trong SSMS → **Execute** (hoặc tạo DB `DocumentSharingDB` thủ công).

**Cách B – dùng EF Core (nếu dự án bật migrations)**
```powershell
# Package Manager Console
Add-Migration Init
Update-Database
