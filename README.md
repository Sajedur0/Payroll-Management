<p align="center">
  <img src="assets/about-icon.png" width="180" alt="Payroll Management Logo"/>
</p>

<h1 align="center">💼 Payroll Management</h1>

<p align="center">
  <b>A Modern WPF Payroll & Attendance Management System for Windows</b><br/>
  Employee • Attendance • Reports • All in One Dashboard
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET 8"/>
  <img src="https://img.shields.io/badge/WPF-Windows-0078D6?style=for-the-badge&logo=windows&logoColor=white" alt="WPF"/>
  <img src="https://img.shields.io/badge/SQL_Server-2019--2022-CC2927?style=for-the-badge&logo=microsoftsqlserver&logoColor=white" alt="SQL Server"/>
  <img src="https://img.shields.io/badge/License-MIT-green?style=for-the-badge" alt="License"/>
  <img src="https://img.shields.io/badge/Platform-Windows-00A4EF?style=for-the-badge" alt="Platform"/>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/C%23-239120?style=flat&logo=csharp&logoColor=white" alt="C#"/>
  <img src="https://img.shields.io/badge/MVVM-Architecture-blueviolet?style=flat" alt="MVVM"/>
  <img src="https://img.shields.io/badge/LiveCharts-2.0-FF6B6B?style=flat" alt="LiveCharts"/>
  <img src="https://img.shields.io/badge/ClosedXML-Excel-21A366?style=flat&logo=microsoft-excel&logoColor=white" alt="ClosedXML"/>
</p>

---

## 📑 Table of Contents

- [About](#-about)
- [✨ Features](#-features)
- [🖼️ Screenshots](#️-screenshots)
- [🛠️ Tech Stack](#️-tech-stack)
- [📁 Project Structure](#-project-structure)
- [🚀 Getting Started](#-getting-started)
- [🗄️ Database Setup](#️-database-setup)
- [⚙️ Configuration](#️-configuration)
- [📖 Usage Guide](#-usage-guide)
- [👨‍💻 Developer](#-developer)
- [📜 License](#-license)

---

## 📌 About

**Payroll Management** is a powerful desktop application built with **WPF (.NET 8)** for managing employees, tracking attendance, and generating payroll reports. Designed for HR departments and small-to-medium businesses, it provides a clean, modern UI with real-time data from **Microsoft SQL Server**.

> Formerly *Attendance Management System* — now rebranded as **Payroll Management** with a complete UI/UX overhaul, payroll focus, and hide-internal-database philosophy (no table/database names exposed to end-users).

### Why Payroll Management?

- 🎯 **All-in-One** — Employees, Attendance, Reports, Admin Tools in a single window
- ⚡ **Real-Time** — Live data from SQL Server with fallback connection logic
- 📊 **Visual** — Weekly attendance trends with LiveCharts
- 📥 **Fast Import** — Bulk employee import from Excel (.xlsx/.xls)
- 🔒 **Secure** — No internal DB/table names shown to users; clean user experience
- 🎨 **Modern UI** — Rounded cards, soft shadows, indigo sidebar (`#1E1B4B`) and responsive layout

---

## ✨ Features

### 📊 Dashboard
- Summary Cards: **Total Employees / Present Today / Absent Today / On Leave**
- Weekly Attendance Trend chart (LiveChartsCore — SkiaSharp)
- Recent Activity feed (last 5 logs)
- Auto-refresh + Last refresh timestamp
- Fallback dummy data if DB is unreachable

### 👥 Employee Management
- Full **CRUD** — Add / Edit / Delete employees
- **Excel Import** — `ClosedXML` powered import with Upsert logic (insert or update by `EmpID`)
- Live search (Name, EmpID, Department, Designation, NID, RocketAC)
- Detailed form with 18 fields: Name, EmpID, Gender, Designation, Section, Department, Shift, Category, Status, RocketAC, GrossWages, Religion, DOJ, FatherName, NID, addresses
- Validation (required fields, EmpID uniqueness)

### 📋 Attendance Log
- Live attendance records with **Face ID sync** (`KQZ_Card` → `RawData`)
- Date range filter (`From` / `To`) + EmpID search
- **Progress Bar** — Real-time `Processed / Total / Remaining` with percentage
- Batch processing (250 records/batch) + `SqlBulkCopy` + `MERGE` for performance
- Auto-creates `RawData` table and index if missing

### 📈 Report
- Auto-generated summary (same as Dashboard)
- Employee list with key columns (EmpID, Name, Department, Designation, DOJ)
- One-click refresh

### ⚙️ Admin Tools
- System Management overview (no internal names exposed)
- Connection string preview (masked)
- **Test Connection** button with live feedback
- **Open Script Folder** — one-click to SQL script location
- Setup instructions for first-time DB creation
- Tables Overview (user-friendly names, not `dbo.*`)

---

## 🖼️ Screenshots

> Replace the placeholders below with your actual screenshots (`assets/screenshot-*.png`)

| Dashboard | Employee Management |
|:---------:|:-------------------:|
| <img src="assets/about-icon.png" width="400" alt="Dashboard"/> | <img src="assets/about-icon.png" width="400" alt="Employee"/> |

| Attendance Log | Report |
|:--------------:|:------:|
| <img src="assets/about-icon.png" width="400" alt="Attendance"/> | <img src="assets/about-icon.png" width="400" alt="Report"/> |

```text
Tip: Take screenshots with Win+Shift+S and save as:
  assets/screenshot-dashboard.png
  assets/screenshot-employee.png
  assets/screenshot-attendance.png
  assets/screenshot-report.png
```

---

## 🛠️ Tech Stack

| Layer | Technology |
|-------|------------|
| **Framework** | .NET 8.0 (Windows), WPF (`UseWPF=true`) |
| **Language** | C# 12, XAML, MVVM pattern |
| **UI** | WPF ResourceDictionary, Custom `SidebarButton` style, `DropShadowEffect` |
| **Charts** | `LiveChartsCore.SkiaSharpView.WPF` v2.0.0-rc2 |
| **Excel** | `ClosedXML` v0.105.1 |
| **Database** | Microsoft SQL Server, `Microsoft.Data.SqlClient` v5.2.0 |
| **Config** | `System.Configuration.ConfigurationManager` v8.0.0, `App.config` |
| **Icon** | `assets/icon.ico` (256x256, embedded via `<ApplicationIcon>`) |
| **Build** | `Payroll Management.csproj` (Sdk=`Microsoft.NET.Sdk`), `AssemblyName=Payroll Management` |

---

## 📁 Project Structure

```text
Payroll Management/
├── assets/
│   ├── icon.ico          # App icon (Taskbar / EXE)
│   ├── about-icon.png    # GitHub README logo (1080 KB)
│   └── developer.jpg     # Developer photo
├── Data/
│   ├── DbConfig.cs               # ConnectionString + fallback logic + TestConnection
│   └── AttendanceRepository.cs   # All SQL queries (Dashboard, Employees, Attendance)
├── Models/
│   ├── Employee.cs
│   ├── AttendanceRecord.cs
│   ├── RawAttendanceData.cs
│   ├── DashboardSummary.cs
│   └── ...
├── ViewModels/
│   ├── DashboardViewModel.cs
│   ├── EmployeeViewModel.cs
│   ├── AttendanceLogViewModel.cs
│   └── ReportViewModel.cs
├── Views/
│   ├── DashboardView.xaml(.cs)
│   ├── EmployeeView.xaml(.cs)
│   ├── AttendanceLogView.xaml(.cs)
│   ├── ReportView.xaml(.cs)
│   ├── EmployeeFormWindow.xaml(.cs)
│   └── AdminToolsView.xaml(.cs)
├── App.xaml / App.xaml.cs
├── MainWindow.xaml / .cs         # Sidebar + ContentControl navigation
├── App.config                    # connectionStrings (kept internal)
├── Payroll Management.csproj
└── README.md
```

---

## 🚀 Getting Started

### Prerequisites

- **Windows 10/11**
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) (with **.NET desktop development** workload)
- **Microsoft SQL Server** (Express / LocalDB / Full)

### 1. Clone

```bash
git clone https://github.com/Sajedur0/Payroll-Management.git
cd Payroll\ Management
```

### 2. Restore & Build

```bash
dotnet restore "Payroll Management.csproj"
dotnet build "Payroll Management.csproj" -c Release
```

Or open `Payroll Management.csproj` in Visual Studio and press **F5**.

### 3. Run

```bash
dotnet run --project "Payroll Management.csproj"
# or
./bin/Release/net8.0-windows/Payroll\ Management.exe
```

---

## 🗄️ Database Setup

> Database names are **internal only** — users never see them in the UI.

1. Open **SQL Server Management Studio (SSMS)** and connect to `.\SQLEXPRESS` (or `(localdb)\MSSQLLocalDB` / `.`).
2. Open `Database\setup.sql` via **File → Open → File** and click **Execute (F5)**.
3. On success, the system database will be created with sample data.
4. Run the app — Dashboard, Attendance Log and Reports will show live data automatically.
5. For LocalDB, set in `App.config`:

```xml
<connectionStrings>
  <add name="AttendanceDB" 
       connectionString="Server=(localdb)\MSSQLLocalDB;Database=AttendanceDB;Trusted_Connection=True;TrustServerCertificate=True;" />
</connectionStrings>
```

App tries fallback automatically: `.\SQLEXPRESS → (localdb)\MSSQLLocalDB → . → localhost`.

---

## ⚙️ Configuration

**`App.config`** (internal, not shown to users):

```xml
<connectionStrings>
  <add name="AttendanceDB" 
       connectionString="Server=.;Database=AttendanceDB;Trusted_Connection=True;TrustServerCertificate=True;" />
  <add name="FACEIDDB" 
       connectionString="Server=.;Database=FACEIDDB;Trusted_Connection=True;TrustServerCertificate=True;" />
</connectionStrings>
```

- Change `Server=` to your SQL instance.
- Use SQL Auth if needed: `Server=.\SQLEXPRESS;Database=AttendanceDB;User Id=sa;Password=YourPassword;TrustServerCertificate=True;`

**App Icon:** Configured in `Payroll Management.csproj:9`

```xml
<ApplicationIcon>assets\icon.ico</ApplicationIcon>
<Resource Include="assets\icon.ico" />
```

And in `MainWindow.xaml:7`:

```xml
<Window Icon="assets/icon.ico" Title="Payroll Management" ...>
```

---

## 📖 Usage Guide

### Dashboard

- View **Total / Present / Absent / On Leave** at a glance.
- Weekly chart shows last 7 days attendance.
- Click **⟳ Refresh** to reload from server.
- `Last refresh: 12:34:56 PM` shows sync time.

### Employee

- **🔍 Search** → type name/EmpID/department and press Search.
- **➕ Add Employee** → fill required `Name*`, `EmpID*`, `Department*` and Save.
- **✏️ Edit Selected** → select a row, edit, Update.
- **🗑 Delete Selected** → confirmation dialog, then delete.
- **📥 Import Data from Excel** → select `.xlsx` with headers `EmpID`, `Name`, `Department`, etc. — auto Upsert.

### Attendance Log

- Set **From / To** dates → **Search**.
- **📥 Attendance Data Load** → pulls from external Face ID source with live progress bar.
- Progress shows `Loading 250 of 1000 records (750 remaining)... 25%`.

### Report

- Read-only summary + employee list for printing/export.

### Admin Tools

- **Test Connection** → validates server connectivity.
- **Open SQL Script Folder** → opens `Database/` folder.

---

## 👨‍💻 Developer

<p align="center">
  <img src="assets/about-icon.png" width="140" style="border-radius:50%" alt="Developer"/>
</p>

<p align="center">
  <b>Sajedur Rahman</b><br/>
  <i>Full-Stack Developer • WPF • .NET • SQL Server</i>
</p>

<p align="center">
  <a href="https://github.com/Sajedur0">
    <img src="https://img.shields.io/badge/GitHub-Sajedur0-181717?style=for-the-badge&logo=github&logoColor=white" alt="GitHub"/>
  </a>
  <a href="mailto:31769074+Sajedur0@users.noreply.github.com">
    <img src="https://img.shields.io/badge/Email-Contact-D14836?style=for-the-badge&logo=gmail&logoColor=white" alt="Email"/>
  </a>
</p>

> **Payroll Management** is crafted with ❤️ for HR teams who need a fast, offline, secure Windows solution.  
> Logo & icons: `assets/about-icon.png` (180px, circular, blue-green gradient with payroll symbols).  
> Developer photo: `assets/about-icon.png` (as requested) — replace with `assets/developer.jpg` if you prefer a personal photo:
>
> ```md
> <img src="assets/developer.jpg" width="140" style="border-radius:50%" />
> ```

### Connect

- GitHub: [@Sajedur0](https://github.com/Sajedur0)
- Project: `Payroll Management` — `RootNamespace: PayrollManagement`

---

## 📜 License

This project is licensed under the **MIT License** — feel free to use, modify and distribute.

```
MIT License — Copyright (c) 2026 Sajedur Rahman
```

---

<p align="center">
  <b>⭐ Star this repo if you find it helpful!</b><br/>
  <sub>Made with WPF • C# • SQL Server • LiveCharts • ClosedXML</sub>
</p>

<p align="center">
  <img src="assets/icon.ico" width="32" alt="icon"/>  Payroll Management — <i>Payroll Made Simple</i>
</p>
