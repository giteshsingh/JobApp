# JobApp - AI-Powered Job Search & Application Assistant

## Overview

**JobApp** is an intelligent job search automation tool that uses Claude AI to help you find, analyze, and apply for jobs more effectively. It aggregates job listings from multiple sources, scores them against your profile, and provides AI-powered resume tailoring and cover letter generation.

### What It Does

JobApp streamlines your job search workflow by:

1. **Aggregating Jobs** - Pulls job listings from multiple sources (Remotive, Arbeitnow, Adzuna, JSearch)
2. **AI Triage** - Uses Claude to quickly evaluate job fit based on your resume and preferences
3. **Intelligent Scoring** - Ranks jobs by relevance using both AI analysis and rule-based scoring
4. **Resume Tailoring** - Generates customized resume versions and cover letters for specific jobs using Claude
5. **Application Tracking** - Tracks which jobs you've applied to and their status in the pipeline
6. **Cost Control** - Built-in spend guards to keep Claude API costs under control (with configurable monthly budget)

---

## System Requirements

### Server-Side (Backend)
- **.NET 9.0** or later
- **SQLite** (included, no separate installation needed)
- At least **100 MB** of disk space for database and dependencies

### Client-Side (Frontend)
- **Node.js** 16+ and **npm** (for building and development)
- Modern web browser supporting ES modules

### External Services Required
- **Claude API Key** from Anthropic (https://console.anthropic.com)
- **Optional: Job Aggregation API Keys**
  - Adzuna (free at https://developer.adzuna.com)
  - RapidAPI for JSearch (free tier available at https://rapidapi.com)
  - Remotive and Arbeitnow work without API keys

### Hardware
- **Minimum**: 2GB RAM, 1GB disk space
- **Recommended**: 4GB RAM, 2GB disk space for comfortable operation

---

## Installation & Setup

### 1. Prerequisites
```bash
# Check .NET version
dotnet --version  # Should be 9.0 or later

# Check Node version
node --version    # Should be 16+
npm --version     # Should be 8+
```

### 2. Clone or Extract the Project
```bash
cd JobApp
```

### 3. Install Frontend Dependencies
```bash
cd frontend
npm install
cd ..
```

### 4. Build the Frontend (Required Before Running)
```bash
cd frontend
npm run build
cd ..
```

This creates optimized production assets in `frontend/dist` which the backend serves.

### 5. Configure Claude API Token

The Claude API key is your gateway to AI-powered features. Here are three ways to configure it (in order of preference):

#### **Option A: Environment Variable (Recommended for Production)**
```bash
# Linux/Mac
export ANTHROPIC_API_KEY="sk-ant-..."

# Windows PowerShell
$env:ANTHROPIC_API_KEY = "sk-ant-..."

# Windows Command Prompt
set ANTHROPIC_API_KEY=sk-ant-...
```

Then run the application; it will automatically read the key.

#### **Option B: .NET User Secrets (Recommended for Development)**
```bash
# Navigate to the API project
cd src/JobApp.Api

# Set the secret (only needs to be done once)
dotnet user-secrets set Claude:ApiKey "sk-ant-..."

# Verify it's set
dotnet user-secrets list

cd ../..
```

User secrets are stored securely per-user and never checked into version control.

#### **Option C: appsettings.json (Not Recommended - Security Risk)**
Edit `src/JobApp.Api/appsettings.json`:
```json
{
  "Claude": {
    "ApiKey": "sk-ant-...",  // ⚠️ Never commit this to git!
    ...
  }
}
```

**⚠️ WARNING**: Only use this for local development. Never commit API keys to version control. Add `appsettings.json` to `.gitignore` if using this method.

### 6. Get Your Claude API Key

1. Visit https://console.anthropic.com
2. Create an account or log in
3. Navigate to "API Keys"
4. Click "Create Key"
5. Copy the key (starts with `sk-ant-`)
6. **Keep it safe** — treat it like a password

### 7. Run the Application

#### Development Mode
```bash
cd src/JobApp.Api
dotnet run
```

The application will:
- Start the backend API on `https://localhost:5001`
- Serve the frontend UI from the same port
- Create `jobapp.db` SQLite database on first run
- Import your initial resume (if configured in `appsettings.json`)

#### Production Mode
```bash
cd src/JobApp.Api
dotnet run --configuration Release
```

Access the application at `http://localhost:5000` (or configured URL)

---

## Configuration Guide

### API Configuration (`appsettings.json`)

#### Claude Settings
```json
"Claude": {
  "ApiKey": null,                // Set via environment variable or user secrets (recommended)
  "TriageModel": "claude-haiku-4-5",      // Fast, cheap model for quick job screening
  "TailoringModel": "claude-sonnet-5",    // Powerful model for detailed resume tailoring
  "TriageMaxTokens": 2000,        // Max output tokens per job triage (increase for detailed analysis)
  "TailoringMaxTokens": 16000,    // Max output tokens for resume tailoring
  "MonthlyBudgetUsd": 20,         // Your monthly Claude API spend limit
  "MaxTriagePerCycle": 40         // Max jobs to analyze per polling cycle
}
```

**Model Choice Guidance:**
- **Triage Model**: Keep on `claude-haiku-4-5` for speed and cost efficiency
- **Tailoring Model**: Upgrade to `claude-sonnet-5` for higher quality tailored content

#### Job Sources Configuration
```json
"JobSources": {
  "Adzuna": {
    "AppId": null,      // Get from https://developer.adzuna.com (free)
    "AppKey": null      // Keep null if you don't want Adzuna jobs
  },
  "JSearch": {
    "RapidApiKey": null,                    // Get from RapidAPI (free tier available)
    "Host": "jsearch.p.rapidapi.com"
  },
  "EnableRemotive": true,    // Works without API key
  "EnableArbeitnow": true,   // Works without API key
  "MaxResultsPerSource": 50  // Results per source per poll
}
```

#### Ingestion (Automatic Job Polling)
```json
"Ingestion": {
  "Enabled": true,                // Enable automatic job fetching
  "PollIntervalMinutes": 60,      // How often to check for new jobs (in minutes)
  "StartupDelaySeconds": 15,      // Delay before first poll after startup
  "ArchiveAfterDays": 45          // Auto-archive jobs older than this
}
```

#### Database
```json
"ConnectionStrings": {
  "JobAppDb": "Data Source=jobapp.db"  // SQLite file location
}
```

---

## Project Architecture

### Frontend (`/frontend`)
**Technology**: Vue 3 + Vite

**Key Views:**
- **Dashboard** — Overview of job pipeline and AI usage stats
- **Jobs** — Browse and filter all job listings with AI scores
- **Pipeline** — Track application status (Interested → Applied → Interviewing → Offer → Rejected)
- **Resumes** — Manage and upload multiple resume versions
- **Tailoring** — Generate tailored resume & cover letter for specific jobs
- **Settings** — Configure Claude API and job search preferences

**Communicates with**: Backend API via REST endpoints

---

### Backend API (`/src/JobApp.Api`)
**Technology**: ASP.NET Core 9.0 with Swagger/OpenAPI

**Key Endpoints:**
- `/api/dashboard/*` — Stats, spend tracking, pipeline metrics
- `/api/jobs/*` — CRUD operations on jobs, filtering, scoring
- `/api/ingestion/*` — Manual import, source configuration, polling control
- `/api/profiles/*` — Search profiles management
- `/api/resumes/*` — Resume upload, parsing, retrieval
- `/api/tailoring/*` — Resume tailoring and cover letter generation
- `/api/pipeline/*` — Application pipeline status tracking

**Serves**: Built frontend assets from `/wwwroot` on production

---

### Core Domain (`/src/JobApp.Core`)
**Responsibility**: Business logic and domain models (no dependencies on EF or external libraries)

**Key Classes:**
- `Job` — Job listing entity
- `SearchProfile` — Your preferences, skills, target roles
- `ResumeDoc` — Parsed resume data
- `JobApplication` — Tracks application status
- `JobScore` — Scoring results
- `AiUsageRecord` — Tracks Claude API calls for cost management

**Key Interfaces:**
- `IJobAnalyst` — Contract for AI analysis (Triage, Deep Fit, Tailoring)
- `IJobSource` — Contract for job aggregation sources
- `IResumeImporter` / `IResumeRenderer` — Resume document handling

---

### Infrastructure (`/src/JobApp.Infrastructure`)
**Responsibility**: Implementation details, external service integration, data access

#### AI Module (`/Ai`)
- **ClaudeJobAnalyst** — Implements Claude API integration
  - Triage: Quick 2000-token job evaluation
  - Deep Fit: Detailed analysis with higher token limit
  - Tailoring: Generate tailored resume suggestions
  - ParseJobPosting: Extract structured data from raw job text
  
- **Prompts** — System and user prompts for Claude
- **AiSchemas** — JSON schemas for structured output validation
- **SpendGuard** — Enforces monthly budget limit before making API calls
- **ClaudePricing** — Pricing logic for different models (configurable per model)

#### Data Module (`/Data`)
- **JobAppDbContext** — EF Core database context
- **Migrations** — Database schema management
- **Seeder** — Initial data setup and resume import on first run

#### Services Module (`/Services`)
- **IngestionService** — Polls job sources, parses postings, scores jobs
- **ScoringService** — Multi-factor job scoring (keyword match, seniority, location, etc.)
- **TailoringService** — Orchestrates resume tailoring workflow
- **DashboardService** — Aggregates stats and pipeline data
- **ResumeService** — Resume lifecycle management

#### Documents Module (`/Documents`)
- **DocxResumeImporter** — Parses .docx resume files (extracts text/structure)
- **DocxResumeRenderer** — Generates tailored .docx resumes
- **PdfResumeRenderer** — Generates .pdf versions (uses QuestPDF)
- **ResumeStructureParser** — Identifies resume sections (education, experience, etc.)

#### Job Sources (`/Sources`)
- **AdzunaJobSource** — Aggregates from Adzuna API
- **JSearchJobSource** — Aggregates from RapidAPI JSearch
- **RemotiveJobSource** — Scrapes Remotive.com
- **ArbeitnowJobSource** — Scrapes Arbeitnow.co

---

## How JobApp Helps You

### 1. **Save Time**
- Automatically fetches jobs from multiple sources every hour
- AI pre-screens jobs so you only see relevant opportunities
- One-click tailored resume generation saves hours of manual editing

### 2. **Improve Job Fit**
- Claude analyzes job requirements against your actual experience
- Identifies skill gaps and missing keywords
- Highlights red flags (unrealistic requirements, missing visa sponsorship info, etc.)

### 3. **Increase Success Rate**
- Tailored resumes with relevant keywords boost ATS (Applicant Tracking System) scores
- Custom cover letters show genuine interest
- AI highlights which skills to emphasize for each specific role

### 4. **Control Costs**
- Monthly spend budget prevents runaway API costs
- Uses cheap model (Haiku) for initial screening, expensive model (Sonnet) only when you choose
- Tracks every API call and cost in the Dashboard

### 5. **Stay Organized**
- Pipeline view shows application status at a glance
- Track which jobs you've applied to, interviewed for, or rejected
- Archive old jobs to keep focused on fresh opportunities

---

## Usage Workflow

### First Time Setup
1. Start the application
2. Upload your current resume from the Resumes view
3. Create a search profile (target role, salary, location, preferences)
4. Configure job sources and set your monthly Claude budget
5. Sit back as JobApp automatically pulls and scores jobs

### Daily/Weekly Workflow
1. **Check Dashboard** — See new high-scoring jobs and API spend
2. **Review Jobs** — Look at scored jobs, read AI analysis
3. **Apply to Interesting Jobs**:
   - Click "Tailor Resume" to generate customized version
   - Review Claude's suggestions
   - Download tailored resume and cover letter
   - Manually apply on company website
   - Update pipeline status
4. **Track Progress** — Use Pipeline view to monitor interviews/offers

### Advanced Features
- **Deep Fit Analysis** — For promising jobs, click "Deep Fit" for more detailed analysis
- **Multiple Resumes** — Upload different resume versions for different career paths
- **Spend Tracking** — View exact costs per job analysis in Dashboard
- **Custom Profiles** — Create profiles for different job searches (e.g., "Remote Only", "Senior Roles")

---

## Claude AI Usage & Costs

### What's Analyzed?
- **Every new job** gets triaged automatically (cost: ~$0.01 per job)
- **Your resume** is analyzed once per profile setup
- **Tailored resumes** generated on-demand when you click "Tailor" (cost: ~$0.05-$0.10 per job)

### Cost Examples (as of Sep 2024)
- Triaging 100 jobs: ~$1
- Generating 10 tailored resumes: ~$0.50-$1.00
- Monthly usage (40 jobs triaged + 5 tailored): ~$1-2

### Controlling Costs
1. **Set monthly budget** in Settings (default: $20)
2. **Adjust `MaxTriagePerCycle`** to limit jobs analyzed per poll
3. **Use cheaper model** for triage; only tailor resumes for jobs you're serious about
4. **Monitor Dashboard** — Check API costs in real-time

### Reducing Costs Further
- Increase `PollIntervalMinutes` to poll less frequently
- Lower `MaxResultsPerSource` to get fewer jobs per poll
- Disable sources you don't care about (Adzuna, JSearch)
- Manually trigger tailor only for promising jobs (don't tailor everything)

---

## Troubleshooting

### Application Won't Start
**Error**: "ANTHROPIC_API_KEY not set or invalid"
- **Solution**: Ensure you've configured your Claude API key via environment variable or user secrets
- **Check**: `dotnet user-secrets list` to verify it's set

**Error**: "Could not connect to database"
- **Solution**: Check `appsettings.json` — ensure SQLite connection string is correct
- **Check**: Make sure the `jobapp.db` directory is writable

### No Jobs Appearing
**Problem**: Dashboard is empty after running for an hour
- **Check**: Verify ingestion is enabled: `"Ingestion": { "Enabled": true }`
- **Check**: Job sources are enabled (at least Remotive or Arbeitnow)
- **Check**: `PollIntervalMinutes` — might not have triggered yet
- **Solution**: Manually trigger ingestion from Dashboard "Refresh" button

### API Calls Failing
**Error**: "Claude returned no text content"
- **Cause**: Model hallucinating or returning malformed JSON
- **Solution**: Retry (transient error) or check if job posting is corrupted

**Error**: "Monthly budget exceeded"
- **Cause**: You've hit the configured spend limit
- **Solution**: Increase `MonthlyBudgetUsd` in settings or wait until next month

### Resume Not Imported
**Problem**: Expected resume from `Seed:ResumePath` not found on startup
- **Check**: Verify file path in `appsettings.json` is correct
- **Check**: File is readable and valid .docx format
- **Solution**: Upload resume manually from Resumes view

---

## Development

### Running in Development Mode

**Terminal 1 — Backend API** (auto-reload on code changes)
```bash
cd src/JobApp.Api
dotnet watch run
```

**Terminal 2 — Frontend Dev Server** (auto-reload on file changes)
```bash
cd frontend
npm run dev
```

Frontend will proxy API calls to `http://localhost:5001`

### Building for Production
```bash
# Build frontend
cd frontend
npm run build

# Publish backend
cd ../src/JobApp.Api
dotnet publish -c Release -o ../../publish
```

Then deploy the `publish` folder.

### Database Migrations
```bash
cd src/JobApp.Infrastructure
dotnet ef migrations add YourMigrationName --project ../JobApp.Infrastructure
dotnet ef database update --project ../JobApp.Api
```

---

## Architecture Decisions

### Why .NET 9.0?
- Modern language features (records, nullable reference types, pattern matching)
- Strong typing prevents many bugs
- Excellent performance
- Mature EF Core for data access

### Why SQLite?
- Zero-config, embeddable database
- Perfect for single-user tool
- Migrates easily to PostgreSQL/SQL Server if needed
- No separate database server required

### Why Claude API?
- State-of-the-art reasoning for understanding job requirements
- Structured output (JSON mode) for reliable parsing
- Prompt caching keeps costs low for repeated analysis
- Vision capabilities available for future enhancements

### Why Vue 3?
- Lightweight, fast frontend framework
- Excellent developer experience
- Large ecosystem of components
- Easy to self-host and deploy

---

## License & Attribution

This project uses:
- **Anthropic's Claude API** — AI-powered job analysis
- **.NET Framework** — Open source web platform
- **Vue.js** — Progressive JavaScript framework
- **QuestPDF** — PDF generation (Community License)
- Job sources: Remotive, Arbeitnow, Adzuna, RapidAPI

---

## Next Steps

1. **[Get Claude API Key](https://console.anthropic.com)** — Takes 5 minutes
2. **Configure and Run** — Follow the installation steps above
3. **Upload Resume** — Start with your current resume
4. **Create Profile** — Define your job preferences
5. **Let It Run** — JobApp will automatically find and score jobs

---

## Support & Feedback

For issues, questions, or feature requests:
- Check the troubleshooting section above
- Review Claude API documentation: https://docs.anthropic.com
- Check .NET documentation: https://learn.microsoft.com/dotnet

---

**Built with ❤️ for job seekers who value their time**
