# 🌍 Tierra — Multi-Tenant SaaS Billing & Subscription Platform

> *"Ground your SaaS business on solid billing infrastructure."*

[![.NET Version](https://img.shields.io/badge/.NET-9.0%20LTS-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Architecture](https://img.shields.io/badge/Architecture-Clean%20Multi--Tenant-blue)]()
[![Database](https://img.shields.io/badge/Database-SQL%20Server%20%2F%20SSMS-CC292B?logo=microsoftsqlserver&logoColor=white)](https://www.microsoft.com/sql-server)
[![Background Jobs](https://img.shields.io/badge/Jobs-Hangfire-orange)](https://www.hangfire.io/)
[![Currency](https://img.shields.io/badge/Currency-ZAR%20(Rands)-green)]()

---

## 📖 The Tierra Metaphor

**Tierra** is Spanish/Portuguese for *"earth"* or *"ground."* 

In a multi-tenant B2B SaaS architecture, the database is shared across all customers, but every business (tenant) needs its own secure, isolated plot of land. **Tierra** acts as the foundational bedrock that segments and protects each tenant's data, subscriptions, and billing records without cross-tenant data leaks.

---

## 🏗️ System Architecture & Engineering Highlights

```
                                 [ Incoming Request ]
                                          │
                   ┌──────────────────────┴──────────────────────┐
                   │                                             │
            [ Browser Session ]                          [ API Request ]
            (Tierra_TenantId Cookie)                     (X-Tenant-Id Header)
                   │                                             │
                   └──────────────────────┬──────────────────────┘
                                          ▼
                                ┌───────────────────┐
                                │   TenantService   │
                                │(Ambient Context)  │
                                └─────────┬─────────┘
                                          │ Resolves TenantId
                                          ▼
                                ┌───────────────────┐
                                │  TierraDbContext  │
                                └─────────┬─────────┘
                   ┌──────────────────────┴──────────────────────┐
                   ▼                                             ▼
       [ Global Query Filters ]                       [ Automated Stamping ]
     Auto-appends WHERE TenantId = @id           Injects TenantId on SaveChangesAsync()
                   │                                             │
                   └──────────────────────┬──────────────────────┘
                                          ▼
                         ┌─────────────────────────────────┐
                         │   Microsoft SQL Server (SSMS)   │
                         │           [TierraDb]            │
                         └─────────────────────────────────┘
                                          ▲
                                          │ .IgnoreQueryFilters()
                                          │ (Cross-tenant maintenance)
                                ┌───────────────────┐
                                │     Hangfire      │
                                │ Background Server │
                                └───────────────────┘
```

### 1. Single-Database Column Isolation
- **Approach**: Multi-tenancy is enforced via a single relational database (`TierraDb`) where tenant data is segmented by a `TenantId` foreign key.
- **Contract Pattern**: All tenant-scoped entities (`Subscription`, `Invoice`) implement the `IMustHaveTenant` interface.

### 2. EF Core Global Query Filters (Zero-Leak Guarantee)
- Prevents accidental data leaks by automatically appending `WHERE TenantId = @currentTenant` to every query compiled by Entity Framework Core.
- Developers never need to manually filter by `TenantId` in controllers or services.

### 3. Automated Entity Stamping
- `TierraDbContext` overrides `SaveChangesAsync()` to automatically inject the active `TenantId` onto new entities before persistence, eliminating human omission errors.

### 4. Decoupled Background Processing (Hangfire)
- High-volume tasks (subscription expiration checks, recurring renewals, invoice generation) are decoupled from HTTP request cycles.
- Hangfire uses Microsoft SQL Server as its persistent state store and utilizes `.IgnoreQueryFilters()` to scan across all tenants safely during maintenance runs.

---

## 💳 Billing Tiers & Simulated Webhooks (ZAR)

Tierra implements a tiered subscription model denominated in **South African Rands (ZAR)**:

| Tier | Price / month | Features |
| :--- | :--- | :--- |
| **Free** | `R0.00` | 100 API requests/day, shared tenant plot, community support |
| **Pro** | `R499.00` | Unlimited API requests, automated webhooks, priority email support, 99.9% SLA |
| **Enterprise** | `R1,999.00` | Dedicated database isolation SLA, custom webhooks, dedicated account manager, 99.99% uptime |

*When a tenant upgrades to a paid plan, Tierra simulates payment gateway webhooks by automatically generating a stamped, paid invoice (`INV-YYYYMMDD-XXXX`).*

---

## 📂 Project Structure

```text
Tierra/
├── Controllers/
│   ├── BillingController.cs       # Subscription tier upgrades/downgrades & invoice history
│   ├── HomeController.cs          # Tenant dashboard
│   └── OnboardingController.cs   # Business registration & workspace context switcher
├── Data/
│   └── TierraDbContext.cs         # EF Core DbContext with Global Query Filters & auto-stamping
├── Models/
│   ├── IMustHaveTenant.cs         # Multi-tenant contract interface
│   ├── Invoice.cs                 # Invoicing entity (ZAR amounts, status)
│   ├── OnboardTenantViewModel.cs  # Registration DTO with validation
│   ├── Subscriptions.cs           # Subscription entity (Tiers, billing cycles)
│   └── Tenant.cs                  # Root tenant/organization entity
├── Services/
│   ├── ITenantService.cs          # Tenant resolution contract
│   ├── TenantService.cs           # Ambient tenant resolution (Cookies, Headers, Overrides)
│   └── SubscriptionRenewalJob.cs  # Hangfire background worker using .IgnoreQueryFilters()
├── Views/
│   ├── Billing/Index.cshtml       # Pricing cards & invoice ledger
│   ├── Home/Index.cshtml          # Active tenant dashboard
│   ├── Onboarding/                # Workspace creation & directory switcher
│   └── Shared/_Layout.cshtml      # Global navbar with live workspace pill & Hangfire link
├── appsettings.json               # SQL Server connection string
└── Program.cs                     # Dependency injection, EF Core, Hangfire pipeline
```

---

## 🚀 Quick Start Guide

### Prerequisites
- [.NET 9 SDK](https://dotnet.microsoft.com/)
- [Microsoft SQL Server LocalDB](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb) or SQL Server Express
- [SQL Server Management Studio (SSMS)](https://learn.microsoft.com/en-us/sql/ssms/download-sql-server-management-studio-ssms)

### 1. Clone & Navigate
```powershell
cd Tierra
```

### 2. Configure Database Connection
Check `appsettings.json` to ensure your connection string points to your local SQL Server instance:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=TierraDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
}
```

### 3. Run Database Migrations
```powershell
dotnet ef database update
```

### 4. Launch the Platform
```powershell
dotnet run
```
Navigate to `http://localhost:5001` in your web browser.

---

## 🔍 Hangfire Administration
Access the background worker dashboard live at:
```text
http://localhost:5000/hangfire
```
From here you can view active servers, inspect recurring jobs (`daily-subscription-renewal-check`), view historical execution metrics, and trigger manual job runs.

---

## ⚖️ License
MIT License. Built for enterprise portfolio demonstration.
